using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Ordering;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Application.Tests.Support;

/// <summary>
/// A real <see cref="ApplicationDbContext"/> on an in-memory SQLite database plus substitutes for the outward-facing
/// abstractions. Handlers are tested against real EF behaviour (filters, constraints, concurrency tokens) instead of mocked DbSets.
/// </summary>
public sealed class DbFixture : IDisposable
{
  private readonly SqliteConnection _connection;

  public DbFixture()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    _connection.Open();

    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
      .UseSqlite(_connection)
      .ConfigureWarnings(a => a.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
      .Options;

    Db = new ApplicationDbContext(options, Time);
    Db.Database.EnsureCreated();
    Activity = new ActivityRecorder(Db, CurrentUser, Time);
  }

  public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

  public ApplicationDbContext Db { get; }

  public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

  public IBoardNotifier Notifier { get; } = Substitute.For<IBoardNotifier>();

  public IPositionService Positions { get; } = new PositionService();

  public AuthorizationContext Authorization { get; } = new();

  public ActivityRecorder Activity { get; }

  public T Add<T>(T entity) where T : class
  {
    Db.Add(entity);
    Db.SaveChanges();
    return entity;
  }

  public void ActAs(User user, WorkspaceRole? role = null)
  {
    CurrentUser.UserId.Returns(user.Id);
    if (role is not null)
    {
      Authorization.Role = role.Value;
    }
  }

  /// <summary>Forget everything the context tracks so assertions read what was really persisted.</summary>
  public void ClearTracking() => Db.ChangeTracker.Clear();

  public World CreateWorld()
  {
    var owner = Add(new UserBuilder("Owner").Build());
    var admin = Add(new UserBuilder("Admin").Build());
    var member = Add(new UserBuilder("Member").Build());
    var outsider = Add(new UserBuilder("Outsider").Build());

    var workspace = new WorkspaceBuilder()
      .WithMember(owner, WorkspaceRole.Owner)
      .WithMember(admin, WorkspaceRole.Admin)
      .WithMember(member, WorkspaceRole.Member)
      .Build();
    Add(workspace);

    var board = Add(new BoardBuilder(workspace).Build());
    var todo = Add(new ColumnBuilder(board, "To do").At(1024).Build());
    var doing = Add(new ColumnBuilder(board, "Doing").At(2048).Build());
    var bug = Add(new Label { BoardId = board.Id, Name = "Bug", Color = "#ef4444" });
    var feature = Add(new Label { BoardId = board.Id, Name = "Feature", Color = "#3b82f6" });

    return new World(owner, admin, member, outsider, workspace, board, todo, doing, bug, feature);
  }

  public void Dispose()
  {
    Db.Dispose();
    _connection.Dispose();
  }
}

public sealed record World(
  User Owner,
  User Admin,
  User Member,
  User Outsider,
  Workspace Workspace,
  Board Board,
  Column Todo,
  Column Doing,
  Label Bug,
  Label Feature);
