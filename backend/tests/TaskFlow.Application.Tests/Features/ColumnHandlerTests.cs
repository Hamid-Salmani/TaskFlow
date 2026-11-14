using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Events;
using TaskFlow.Application.Features.Columns;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Features;

public sealed class ColumnHandlerTests : IDisposable
{
  private readonly DbFixture _fixture = new();
  private readonly World _world;

  public ColumnHandlerTests()
  {
    _world = _fixture.CreateWorld();
    _fixture.ActAs(_world.Member, WorkspaceRole.Member);
  }

  public void Dispose() => _fixture.Dispose();

  private CreateColumnCommandHandler CreateHandler() =>
    new(_fixture.Db, _fixture.CurrentUser, _fixture.Positions, _fixture.Activity, _fixture.Notifier);

  private MoveColumnCommandHandler MoveHandler() =>
    new(_fixture.Db, _fixture.CurrentUser, _fixture.Positions, _fixture.Activity, _fixture.Notifier);

  private async Task<List<Column>> ColumnsInOrderAsync()
  {
    _fixture.ClearTracking();
    return await _fixture.Db.Columns.Where(a => a.BoardId == _world.Board.Id).OrderBy(a => a.Position).ToListAsync();
  }

  [Fact]
  public async Task Create_AppendsAtTheEndAndNotifiesTheBoard()
  {
    var result = await CreateHandler().Handle(new CreateColumnCommand(_world.Board.Id, " Review "), default);

    result.Value.Title.Should().Be("Review");
    result.Value.Position.Should().Be(2048 + 1024);
    (await ColumnsInOrderAsync()).Select(a => a.Title).Should().Equal("To do", "Doing", "Review");
    await _fixture.Notifier.Received(1).PublishAsync(
      Arg.Is<BoardEvent>(a => a is ColumnsChangedEvent && a.BoardId == _world.Board.Id && a.ActorUserId == _world.Member.Id),
      Arg.Any<CancellationToken>());
    (await _fixture.Db.ActivityLogs.SingleAsync()).Type.Should().Be(ActivityType.ColumnCreated);
  }

  [Fact]
  public async Task Create_StartsAtTheDefaultStepOnAnEmptyBoard()
  {
    var empty = _fixture.Add(new BoardBuilder(_world.Workspace).Build());

    var result = await CreateHandler().Handle(new CreateColumnCommand(empty.Id, "First"), default);

    result.Value.Position.Should().Be(1024);
  }

  [Fact]
  public async Task Create_IsRejectedOnArchivedBoards()
  {
    _world.Board.IsArchived = true;
    _fixture.Db.SaveChanges();

    var result = await CreateHandler().Handle(new CreateColumnCommand(_world.Board.Id, "Nope"), default);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.Conflict, Code = "board.archived" });
    await _fixture.Notifier.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
  }

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  public void Validators_RejectBlankTitles(string title)
  {
    new CreateColumnCommandValidator().Validate(new CreateColumnCommand(Guid.NewGuid(), title)).IsValid.Should().BeFalse();
    new RenameColumnCommandValidator().Validate(new RenameColumnCommand(Guid.NewGuid(), title)).IsValid.Should().BeFalse();
  }

  [Fact]
  public async Task Rename_ChangesTheTitle()
  {
    var handler = new RenameColumnCommandHandler(_fixture.Db, _fixture.CurrentUser, _fixture.Activity, _fixture.Notifier);

    var result = await handler.Handle(new RenameColumnCommand(_world.Todo.Id, "Backlog"), default);

    result.Value.Title.Should().Be("Backlog");
    (await _fixture.Db.ActivityLogs.SingleAsync()).Message.Should().Contain("To do").And.Contain("Backlog");
  }

  [Fact]
  public async Task Move_PlacesTheColumnBetweenItsNeighbours()
  {
    var third = _fixture.Add(new ColumnBuilder(_world.Board, "Done").At(3072).Build());

    // To do | Doing | Done  ->  Doing | To do | Done  (To do goes after Doing, before Done)
    var result = await MoveHandler().Handle(new MoveColumnCommand(_world.Todo.Id, _world.Doing.Id, third.Id), default);

    result.Value.Position.Should().Be((2048 + 3072) / 2d);
    result.Value.Rebalanced.Should().BeFalse();
    (await ColumnsInOrderAsync()).Select(a => a.Title).Should().Equal("Doing", "To do", "Done");
  }

  [Fact]
  public async Task Move_ToTheStartAndToTheEnd()
  {
    var toStart = await MoveHandler().Handle(new MoveColumnCommand(_world.Doing.Id, null, _world.Todo.Id), default);
    toStart.Value.Position.Should().Be(1024 - 1024);

    _fixture.ClearTracking();
    var toEnd = await MoveHandler().Handle(new MoveColumnCommand(_world.Doing.Id, null, null), default);

    toEnd.Value.Position.Should().Be(1024 + 1024);
    (await ColumnsInOrderAsync()).Select(a => a.Title).Should().Equal("To do", "Doing");
  }

  [Fact]
  public async Task Move_RebalancesWhenTheGapIsExhausted()
  {
    _world.Doing.Position = _world.Todo.Position + 1e-9;
    _fixture.Db.SaveChanges();
    var last = _fixture.Add(new ColumnBuilder(_world.Board, "Last").At(5000).Build());

    var result = await MoveHandler().Handle(new MoveColumnCommand(last.Id, _world.Todo.Id, _world.Doing.Id), default);

    result.Value.Rebalanced.Should().BeTrue();
    var ordered = await ColumnsInOrderAsync();
    ordered.Select(a => a.Title).Should().Equal("To do", "Last", "Doing");
    ordered.Select(a => a.Position).Should().OnlyHaveUniqueItems().And.BeInAscendingOrder();
    ordered.Zip(ordered.Skip(1), (a, b) => b.Position - a.Position).Should().OnlyContain(gap => gap > 1);
  }

  [Fact]
  public async Task Move_RejectsNeighboursFromAnotherBoardOrInTheWrongOrder()
  {
    var other = _fixture.Add(new BoardBuilder(_world.Workspace).Build());
    var foreign = _fixture.Add(new ColumnBuilder(other, "Foreign").Build());

    var outside = await MoveHandler().Handle(new MoveColumnCommand(_world.Todo.Id, foreign.Id, null), default);
    var reversed = await MoveHandler().Handle(new MoveColumnCommand(_world.Todo.Id, _world.Doing.Id, _world.Todo.Id), default);

    outside.Error!.Code.Should().Be("position.neighbor_not_found");
    reversed.Error!.Code.Should().Be("position.neighbor_not_found", "the moving column itself is not a valid neighbour");
  }

  [Fact]
  public async Task Delete_SoftDeletesTheColumnAndItsCards()
  {
    var card = _fixture.Add(new CardBuilder(_world.Board, _world.Todo, _world.Owner).Build());
    var survivor = _fixture.Add(new CardBuilder(_world.Board, _world.Doing, _world.Owner).Build());
    var handler = new DeleteColumnCommandHandler(_fixture.Db, _fixture.CurrentUser, _fixture.Time, _fixture.Activity, _fixture.Notifier);

    var result = await handler.Handle(new DeleteColumnCommand(_world.Todo.Id), default);

    result.IsSuccess.Should().BeTrue();
    _fixture.ClearTracking();
    (await _fixture.Db.Columns.Select(a => a.Id).ToListAsync()).Should().Equal(_world.Doing.Id);
    (await _fixture.Db.Cards.Select(a => a.Id).ToListAsync()).Should().Equal(survivor.Id);
    (await _fixture.Db.Cards.IgnoreQueryFilters().SingleAsync(a => a.Id == card.Id)).IsDeleted.Should().BeTrue();
  }
}
