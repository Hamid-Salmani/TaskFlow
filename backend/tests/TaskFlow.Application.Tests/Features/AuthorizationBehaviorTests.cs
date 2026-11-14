using FluentAssertions;
using MediatR;
using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Features;

public sealed class AuthorizationBehaviorTests : IDisposable
{
  private readonly DbFixture _fixture = new();
  private readonly World _world;
  private readonly AuthorizationBehavior<Probe, Result<string>> _behavior;
  private readonly AuthorizationContext _context = new();

  public AuthorizationBehaviorTests()
  {
    _world = _fixture.CreateWorld();
    _behavior = new AuthorizationBehavior<Probe, Result<string>>(new AccessChecker(_fixture.Db), _fixture.CurrentUser, _context);
  }

  public void Dispose() => _fixture.Dispose();

  private sealed record Probe(ResourceScope Scope, WorkspaceRole MinimumRole) : IRequest<Result<string>>, IAuthorizedRequest;

  private sealed record Unprotected : IRequest<Result<string>>;

  private Task<Result<string>> Run(User actor, ResourceScope scope, WorkspaceRole minimum, out Func<bool> reachedHandler)
  {
    _fixture.ActAs(actor);
    var reached = false;
    reachedHandler = () => reached;
    return _behavior.Handle(
      new Probe(scope, minimum),
      () =>
      {
        reached = true;
        return Task.FromResult<Result<string>>("ok");
      },
      default);
  }

  [Fact]
  public async Task Members_WithEnoughRole_ReachTheHandlerAndTheContextIsFilled()
  {
    var result = await Run(_world.Admin, ResourceScope.Board(_world.Board.Id), WorkspaceRole.Admin, out var reached);

    result.IsSuccess.Should().BeTrue();
    reached().Should().BeTrue();
    _context.Should().BeEquivalentTo(new { WorkspaceId = _world.Workspace.Id, Role = WorkspaceRole.Admin });
  }

  [Fact]
  public async Task Members_BelowTheMinimumRole_GetForbidden()
  {
    var result = await Run(_world.Member, ResourceScope.Board(_world.Board.Id), WorkspaceRole.Admin, out var reached);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.Forbidden, Code = "resource.forbidden" });
    reached().Should().BeFalse();
  }

  [Fact]
  public async Task NonMembers_GetNotFoundSoExistenceIsNotLeaked()
  {
    var result = await Run(_world.Outsider, ResourceScope.Board(_world.Board.Id), WorkspaceRole.Member, out var reached);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.NotFound, Code = "resource.not_found" });
    reached().Should().BeFalse();
  }

  [Fact]
  public async Task MissingResources_LookExactlyLikeForeignOnes()
  {
    var result = await Run(_world.Owner, ResourceScope.Board(Guid.NewGuid()), WorkspaceRole.Member, out _);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.NotFound, Code = "resource.not_found" });
  }

  [Theory]
  [InlineData(ScopeKind.Workspace)]
  [InlineData(ScopeKind.Board)]
  [InlineData(ScopeKind.Column)]
  [InlineData(ScopeKind.Card)]
  [InlineData(ScopeKind.Label)]
  public async Task EveryScopeKind_ResolvesToTheOwningWorkspace(ScopeKind kind)
  {
    var card = _fixture.Add(new CardBuilder(_world.Board, _world.Todo, _world.Owner).Build());
    var id = kind switch
    {
      ScopeKind.Workspace => _world.Workspace.Id,
      ScopeKind.Board => _world.Board.Id,
      ScopeKind.Column => _world.Todo.Id,
      ScopeKind.Card => card.Id,
      _ => _world.Bug.Id
    };

    var result = await Run(_world.Member, new ResourceScope(kind, id), WorkspaceRole.Member, out var reached);

    result.IsSuccess.Should().BeTrue();
    reached().Should().BeTrue();
  }

  [Fact]
  public async Task DeletingABoard_HidesItsCardsFromEveryone()
  {
    var card = _fixture.Add(new CardBuilder(_world.Board, _world.Todo, _world.Owner).Build());
    _world.Board.SoftDelete(DateTime.UtcNow);
    _fixture.Db.SaveChanges();

    var result = await Run(_world.Owner, ResourceScope.Card(card.Id), WorkspaceRole.Member, out _);

    result.Error!.Code.Should().Be("resource.not_found");
  }

  [Fact]
  public async Task DeletingAWorkspace_HidesItsBoards()
  {
    _world.Workspace.SoftDelete(DateTime.UtcNow);
    _fixture.Db.SaveChanges();

    var result = await Run(_world.Owner, ResourceScope.Board(_world.Board.Id), WorkspaceRole.Member, out _);

    result.Error!.Code.Should().Be("resource.not_found");
  }

  [Fact]
  public async Task RequestsWithoutScope_PassThrough()
  {
    var behavior = new AuthorizationBehavior<Unprotected, Result<string>>(new AccessChecker(_fixture.Db), _fixture.CurrentUser, _context);

    var result = await behavior.Handle(new Unprotected(), () => Task.FromResult<Result<string>>("ok"), default);

    result.Value.Should().Be("ok");
  }
}
