using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Auth;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Tests.Features;

public sealed class AuthHandlerTests : IDisposable
{
  private static readonly UserDto Alice = new(Guid.NewGuid(), "alice@test.dev", "Alice", "alice");

  private readonly DbFixture _fixture = new();
  private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
  private readonly IAuthTokenService _tokens = Substitute.For<IAuthTokenService>();
  private readonly SessionIssuer _sessions;
  private int _tokenCounter;

  public AuthHandlerTests()
  {
    _tokens.RefreshTokenLifetime.Returns(TimeSpan.FromDays(14));
    _tokens.CreateAccessToken(Arg.Any<UserDto>()).Returns(new AccessToken("access", new DateTime(2026, 1, 15, 12, 15, 0, DateTimeKind.Utc)));
    _tokens.GenerateRefreshToken().Returns(_ => $"refresh-{++_tokenCounter}");
    _tokens.HashRefreshToken(Arg.Any<string>()).Returns(call => $"hash-of-{call.Arg<string>()}");
    _sessions = new SessionIssuer(_fixture.Db, _tokens, _fixture.Time);

    // RefreshTokens.UserId is a foreign key, so the user has to exist in the database.
    _fixture.Add(new User { Id = Alice.Id, UserName = Alice.Email, Email = Alice.Email, DisplayName = "Alice", Handle = "alice" });
  }

  public void Dispose() => _fixture.Dispose();

  [Fact]
  public async Task Register_ReturnsSessionAndStoresHashedRefreshToken()
  {
    _identity.RegisterAsync("alice@test.dev", "Alice", "Password1", Arg.Any<CancellationToken>()).Returns(Alice);
    var handler = new RegisterCommandHandler(_identity, _sessions);

    var result = await handler.Handle(new RegisterCommand("alice@test.dev", "Alice", "Password1"), default);

    result.IsSuccess.Should().BeTrue();
    result.Value.RefreshToken.Should().Be("refresh-1");
    result.Value.User.Should().Be(Alice);
    var stored = await _fixture.Db.RefreshTokens.SingleAsync();
    stored.TokenHash.Should().Be("hash-of-refresh-1").And.NotBe(result.Value.RefreshToken);
    stored.ExpiresAt.Should().Be(_fixture.Time.GetUtcNow().UtcDateTime.AddDays(14));
  }

  [Fact]
  public async Task Register_PropagatesIdentityFailure()
  {
    _identity.RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns(Error.Conflict("auth.email_taken", "taken"));

    var result = await new RegisterCommandHandler(_identity, _sessions).Handle(new RegisterCommand("a@b.dev", "A", "Password1"), default);

    result.Error!.Code.Should().Be("auth.email_taken");
    (await _fixture.Db.RefreshTokens.CountAsync()).Should().Be(0);
  }

  [Fact]
  public async Task Login_WithValidCredentials_IssuesSession()
  {
    _identity.ValidateCredentialsAsync("alice@test.dev", "Password1", Arg.Any<CancellationToken>()).Returns(Alice);

    var result = await new LoginCommandHandler(_identity, _sessions).Handle(new LoginCommand("alice@test.dev", "Password1"), default);

    result.IsSuccess.Should().BeTrue();
    result.Value.AccessToken.Should().Be("access");
  }

  [Fact]
  public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
  {
    _identity.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns(Error.Unauthorized("auth.invalid_credentials", "bad"));

    var result = await new LoginCommandHandler(_identity, _sessions).Handle(new LoginCommand("alice@test.dev", "nope"), default);

    result.Error!.Type.Should().Be(ErrorType.Unauthorized);
  }

  [Fact]
  public async Task Refresh_RotatesTokenAndKeepsFamily()
  {
    var first = await IssueInitialSessionAsync();
    var handler = CreateRefreshHandler();

    var result = await handler.Handle(new RefreshCommand(first.RefreshToken), default);

    result.IsSuccess.Should().BeTrue();
    result.Value.RefreshToken.Should().NotBe(first.RefreshToken);
    var tokens = await _fixture.Db.RefreshTokens.OrderBy(a => a.CreatedAt).ToListAsync();
    tokens.Should().HaveCount(2);
    tokens[0].RevokedAt.Should().NotBeNull();
    tokens[0].ReplacedByTokenHash.Should().Be(tokens[1].TokenHash);
    tokens[1].RevokedAt.Should().BeNull();
    tokens[1].FamilyId.Should().Be(tokens[0].FamilyId);
  }

  [Fact]
  public async Task Refresh_WithAlreadyRotatedToken_RevokesWholeFamily()
  {
    var first = await IssueInitialSessionAsync();
    var handler = CreateRefreshHandler();
    var second = await handler.Handle(new RefreshCommand(first.RefreshToken), default);

    var reuse = await handler.Handle(new RefreshCommand(first.RefreshToken), default);

    reuse.Error!.Code.Should().Be("auth.refresh_token_reused");
    (await _fixture.Db.RefreshTokens.AllAsync(a => a.RevokedAt != null)).Should().BeTrue();
    var afterwards = await handler.Handle(new RefreshCommand(second.Value.RefreshToken), default);
    afterwards.IsFailure.Should().BeTrue();
  }

  [Fact]
  public async Task Refresh_WithUnknownToken_ReturnsUnauthorized()
  {
    var result = await CreateRefreshHandler().Handle(new RefreshCommand("unknown"), default);

    result.Error!.Code.Should().Be("auth.invalid_refresh_token");
  }

  [Fact]
  public async Task Refresh_WithExpiredToken_ReturnsUnauthorized()
  {
    var first = await IssueInitialSessionAsync();
    _fixture.Time.Advance(TimeSpan.FromDays(15));

    var result = await CreateRefreshHandler().Handle(new RefreshCommand(first.RefreshToken), default);

    result.Error!.Code.Should().Be("auth.refresh_token_expired");
  }

  [Fact]
  public async Task Refresh_WhenUserWasDeleted_ReturnsUnauthorized()
  {
    var first = await IssueInitialSessionAsync();
    var handler = CreateRefreshHandler();
    _identity.GetUserAsync(Alice.Id, Arg.Any<CancellationToken>()).Returns((UserDto?)null);

    var result = await handler.Handle(new RefreshCommand(first.RefreshToken), default);

    result.Error!.Type.Should().Be(ErrorType.Unauthorized);
  }

  [Fact]
  public async Task Logout_RevokesTheTokenFamily()
  {
    var first = await IssueInitialSessionAsync();
    var handler = new LogoutCommandHandler(_fixture.Db, _tokens, _fixture.Time);

    var result = await handler.Handle(new LogoutCommand(first.RefreshToken), default);

    result.IsSuccess.Should().BeTrue();
    (await _fixture.Db.RefreshTokens.SingleAsync()).RevokedAt.Should().NotBeNull();
  }

  [Fact]
  public async Task Logout_WithUnknownToken_StillSucceeds()
  {
    var handler = new LogoutCommandHandler(_fixture.Db, _tokens, _fixture.Time);

    (await handler.Handle(new LogoutCommand("unknown"), default)).IsSuccess.Should().BeTrue();
  }

  [Fact]
  public async Task Me_ReturnsCurrentUser()
  {
    _fixture.CurrentUser.UserId.Returns(Alice.Id);
    _identity.GetUserAsync(Alice.Id, Arg.Any<CancellationToken>()).Returns(Alice);

    var result = await new GetMeQueryHandler(_identity, _fixture.CurrentUser).Handle(new GetMeQuery(), default);

    result.Value.Should().Be(Alice);
  }

  [Fact]
  public async Task Me_WhenUserIsGone_ReturnsNotFound()
  {
    _fixture.CurrentUser.UserId.Returns(Guid.NewGuid());
    _identity.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((UserDto?)null);

    var result = await new GetMeQueryHandler(_identity, _fixture.CurrentUser).Handle(new GetMeQuery(), default);

    result.Error!.Type.Should().Be(ErrorType.NotFound);
  }

  [Theory]
  [InlineData("alice@test.dev", "Alice", "Password1", true)]
  [InlineData("not-an-email", "Alice", "Password1", false)]
  [InlineData("alice@test.dev", "", "Password1", false)]
  [InlineData("alice@test.dev", "Alice", "short1", false)]
  [InlineData("alice@test.dev", "Alice", "NoDigitsHere", false)]
  [InlineData("alice@test.dev", "Alice", "12345678", false)]
  public void RegisterValidator_ChecksEmailNameAndPassword(string email, string name, string password, bool valid)
  {
    var result = new RegisterCommandValidator().Validate(new RegisterCommand(email, name, password));

    result.IsValid.Should().Be(valid);
  }

  [Fact]
  public void LoginAndRefreshValidators_RequireValues()
  {
    new LoginCommandValidator().Validate(new LoginCommand("", "")).IsValid.Should().BeFalse();
    new RefreshCommandValidator().Validate(new RefreshCommand("")).IsValid.Should().BeFalse();
    new RefreshCommandValidator().Validate(new RefreshCommand("token")).IsValid.Should().BeTrue();
  }

  private RefreshCommandHandler CreateRefreshHandler()
  {
    _identity.GetUserAsync(Alice.Id, Arg.Any<CancellationToken>()).Returns(Alice);
    return new RefreshCommandHandler(_fixture.Db, _tokens, _identity, _sessions, _fixture.Time);
  }

  private async Task<AuthResponse> IssueInitialSessionAsync() =>
    await _sessions.IssueAsync(Alice, null, null, default);
}
