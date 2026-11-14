using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.IntegrationTests.Support;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Api.IntegrationTests;

public sealed class AuthTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
  private static string NewEmail() => $"{Guid.NewGuid():N}@test.dev";

  [Fact]
  public async Task Health_ReportsHealthy()
  {
    var response = await factory.CreateClient().GetAsync("/health");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
  }

  [Fact]
  public async Task Register_ReturnsTokens_AndTheAccessTokenWorksForMe()
  {
    var client = factory.CreateClient();
    var email = NewEmail();

    var response = await client.PostJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Ada Lovelace", "Password123"));

    response.StatusCode.Should().Be(HttpStatusCode.Created);
    var auth = await response.ReadAsAsync<AuthResponse>();
    auth.User.Email.Should().Be(email);
    auth.User.Handle.Should().NotBeNullOrEmpty();
    auth.RefreshToken.Should().NotBeNullOrEmpty();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    var me = await client.GetAsync("/api/v1/auth/me");
    me.StatusCode.Should().Be(HttpStatusCode.OK);
    (await me.ReadAsAsync<UserDto>()).DisplayName.Should().Be("Ada Lovelace");
  }

  [Fact]
  public async Task Register_WithDuplicateEmail_ReturnsConflictProblem()
  {
    var client = factory.CreateClient();
    var email = NewEmail();
    (await client.PostJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "First", "Password123"))).EnsureSuccessStatusCode();

    var response = await client.PostJsonAsync("/api/v1/auth/register", new RegisterRequest(email.ToUpperInvariant(), "Second", "Password123"));

    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    (await response.ReadProblemAsync()).Code.Should().Be("auth.email_taken");
  }

  [Fact]
  public async Task Register_WithWeakPassword_ReturnsValidationErrorsKeyedByField()
  {
    var response = await factory.CreateClient().PostJsonAsync("/api/v1/auth/register", new RegisterRequest(NewEmail(), "Weak", "short"));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    var problem = await response.ReadProblemAsync();
    problem.Status.Should().Be(400);
    problem.Errors.Should().ContainKey("password");
  }

  [Fact]
  public async Task Login_WithWrongPassword_ReturnsUnauthorized()
  {
    var user = await factory.RegisterUserAsync();

    var response = await factory.CreateClient().PostJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, "WrongPassword1"));

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    (await response.ReadProblemAsync()).Code.Should().Be("auth.invalid_credentials");
  }

  [Fact]
  public async Task Login_WithCorrectPassword_ReturnsSession()
  {
    var user = await factory.RegisterUserAsync();

    var response = await factory.CreateClient().PostJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, FactoryExtensions.Password));

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    (await response.ReadAsAsync<AuthResponse>()).User.Id.Should().Be(user.Id);
  }

  [Fact]
  public async Task Refresh_RotatesTheToken_AndReusingTheOldOneRevokesTheFamily()
  {
    var client = factory.CreateClient();
    var user = await factory.RegisterUserAsync();
    var oldRefresh = user.Auth.RefreshToken;

    var rotated = await client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(oldRefresh));
    rotated.StatusCode.Should().Be(HttpStatusCode.OK);
    var second = await rotated.ReadAsAsync<AuthResponse>();
    second.RefreshToken.Should().NotBe(oldRefresh);

    var reuse = await client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(oldRefresh));
    reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    (await reuse.ReadProblemAsync()).Code.Should().Be("auth.refresh_token_reused");

    // The legitimate holder of the newest token is signed out as well: the whole family is dead.
    var afterReuse = await client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(second.RefreshToken));
    afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Refresh_WithGarbage_ReturnsUnauthorized()
  {
    var response = await factory.CreateClient().PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest("not-a-token"));

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Logout_InvalidatesTheRefreshToken()
  {
    var client = factory.CreateClient();
    var user = await factory.RegisterUserAsync();

    (await client.PostJsonAsync("/api/v1/auth/logout", new RefreshRequest(user.Auth.RefreshToken))).StatusCode
      .Should().Be(HttpStatusCode.NoContent);

    (await client.PostJsonAsync("/api/v1/auth/refresh", new RefreshRequest(user.Auth.RefreshToken))).StatusCode
      .Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Me_WithoutToken_ReturnsUnauthorized()
  {
    (await factory.CreateClient().GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Me_WithTamperedToken_ReturnsUnauthorized()
  {
    var client = factory.CreateClient();
    var user = await factory.RegisterUserAsync();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Auth.AccessToken + "x");

    (await client.GetAsync("/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task AuthEndpoints_AreRateLimited()
  {
    using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:AuthPermitLimit", "2"));
    var client = limited.CreateClient();
    var request = new LoginRequest("nobody@test.dev", "Password123");

    (await client.PostJsonAsync("/api/v1/auth/login", request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    (await client.PostJsonAsync("/api/v1/auth/login", request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    var third = await client.PostJsonAsync("/api/v1/auth/login", request);

    third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    (await third.ReadProblemAsync()).Code.Should().Be("rate_limit.exceeded");
  }
}
