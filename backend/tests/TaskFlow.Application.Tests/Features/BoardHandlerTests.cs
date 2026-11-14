using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Features.Boards;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Features;

public sealed class BoardHandlerTests : IDisposable
{
  private readonly DbFixture _fixture = new();
  private readonly World _world;

  public BoardHandlerTests() => _world = _fixture.CreateWorld();

  public void Dispose() => _fixture.Dispose();

  [Fact]
  public async Task Create_AddsDefaultColumnsInOrderAndLogsActivity()
  {
    _fixture.ActAs(_world.Member);
    var handler = new CreateBoardCommandHandler(_fixture.Db, _fixture.Positions, _fixture.Activity);

    var result = await handler.Handle(new CreateBoardCommand(_world.Workspace.Id, " Sprint ", null), default);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().BeEquivalentTo(new { Name = "Sprint", WorkspaceId = _world.Workspace.Id, IsArchived = false });
    var columns = await _fixture.Db.Columns.Where(a => a.BoardId == result.Value.Id).OrderBy(a => a.Position).ToListAsync();
    columns.Select(a => a.Title).Should().Equal("To do", "In progress", "Done");
    columns.Select(a => a.Position).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    var log = await _fixture.Db.ActivityLogs.SingleAsync(a => a.BoardId == result.Value.Id);
    log.Should().BeEquivalentTo(new { Type = ActivityType.BoardCreated, UserId = _world.Member.Id });
  }

  [Fact]
  public void CreateValidator_RejectsBlankAndOverlongNames()
  {
    var validator = new CreateBoardCommandValidator();
    var id = Guid.NewGuid();

    validator.Validate(new CreateBoardCommand(id, "", null)).IsValid.Should().BeFalse();
    validator.Validate(new CreateBoardCommand(id, new string('x', 101), null)).IsValid.Should().BeFalse();
    validator.Validate(new CreateBoardCommand(id, "Fine", new string('x', 1001))).IsValid.Should().BeFalse();
    validator.Validate(new CreateBoardCommand(id, "Fine", null)).IsValid.Should().BeTrue();
  }

  [Fact]
  public async Task List_HidesArchivedBoardsUnlessAsked()
  {
    _world.Board.IsArchived = true;
    _fixture.Add(new BoardBuilder(_world.Workspace).Build());
    _fixture.Db.SaveChanges();
    var handler = new ListBoardsQueryHandler(_fixture.Db);

    var active = await handler.Handle(new ListBoardsQuery(_world.Workspace.Id, false), default);
    var all = await handler.Handle(new ListBoardsQuery(_world.Workspace.Id, true), default);

    active.Value.Should().ContainSingle().Which.IsArchived.Should().BeFalse();
    all.Value.Should().HaveCount(2);
    all.Value.Select(a => a.IsArchived).Should().Equal(false, true);
  }

  [Fact]
  public async Task Get_ReturnsOrderedColumnsLabelsMembersAndTheCallersRole()
  {
    _fixture.ActAs(_world.Member, WorkspaceRole.Member);
    var handler = new GetBoardQueryHandler(_fixture.Db, _fixture.Authorization);

    var result = await handler.Handle(new GetBoardQuery(_world.Board.Id), default);

    var board = result.Value;
    board.MyRole.Should().Be(WorkspaceRole.Member);
    board.Columns.Select(a => a.Title).Should().Equal("To do", "Doing");
    board.Labels.Select(a => a.Name).Should().Equal("Bug", "Feature");
    board.Members.Should().HaveCount(3);
  }

  [Fact]
  public async Task Get_DoesNotIncludeColumnsOrLabelsOfOtherBoards()
  {
    var other = _fixture.Add(new BoardBuilder(_world.Workspace).Build());
    _fixture.Add(new ColumnBuilder(other, "Elsewhere").Build());
    _fixture.ActAs(_world.Member, WorkspaceRole.Member);

    var result = await new GetBoardQueryHandler(_fixture.Db, _fixture.Authorization).Handle(new GetBoardQuery(_world.Board.Id), default);

    result.Value.Columns.Should().NotContain(a => a.Title == "Elsewhere");
  }

  [Fact]
  public async Task Update_RenamesTheBoard()
  {
    var result = await new UpdateBoardCommandHandler(_fixture.Db)
      .Handle(new UpdateBoardCommand(_world.Board.Id, " Renamed ", "desc"), default);

    result.Value.Should().BeEquivalentTo(new { Name = "Renamed", Description = "desc" });
  }

  [Fact]
  public async Task Archive_TogglesTheFlag()
  {
    var handler = new ArchiveBoardCommandHandler(_fixture.Db);

    (await handler.Handle(new ArchiveBoardCommand(_world.Board.Id, true), default)).Value.IsArchived.Should().BeTrue();
    (await handler.Handle(new ArchiveBoardCommand(_world.Board.Id, false), default)).Value.IsArchived.Should().BeFalse();
  }

  [Fact]
  public async Task Delete_SoftDeletesTheBoard()
  {
    var result = await new DeleteBoardCommandHandler(_fixture.Db, _fixture.Time).Handle(new DeleteBoardCommand(_world.Board.Id), default);

    result.IsSuccess.Should().BeTrue();
    _fixture.ClearTracking();
    (await _fixture.Db.Boards.AnyAsync()).Should().BeFalse();
    (await _fixture.Db.Boards.IgnoreQueryFilters().SingleAsync()).IsDeleted.Should().BeTrue();
  }
}
