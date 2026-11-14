using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Events;
using TaskFlow.Application.Features.Cards;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Features;

public sealed class CardHandlerTests : IDisposable
{
  private readonly DbFixture _fixture = new();
  private readonly World _world;

  public CardHandlerTests()
  {
    _world = _fixture.CreateWorld();
    _fixture.ActAs(_world.Member, WorkspaceRole.Member);
  }

  public void Dispose() => _fixture.Dispose();

  private Card AddCard(Column? column = null, double position = 1024, string title = "Card") =>
    _fixture.Add(new CardBuilder(_world.Board, column ?? _world.Todo, _world.Owner).At(position).Titled(title).Build());

  private async Task<Card> ReloadAsync(Guid id)
  {
    _fixture.ClearTracking();
    return await _fixture.Db.Cards.AsNoTracking().SingleAsync(a => a.Id == id);
  }

  private Task<Result<MoveCardResultDto>> Move(Card card, Column target, Guid? after, Guid? before, int? version = null) =>
    new MoveCardCommandHandler(_fixture.Db, _fixture.CurrentUser, _fixture.Positions, _fixture.Activity, _fixture.Notifier)
      .Handle(new MoveCardCommand(card.Id, version ?? card.Version, target.Id, after, before), default);

  private async Task<List<Guid>> ColumnOrderAsync(Column column)
  {
    _fixture.ClearTracking();
    return await _fixture.Db.Cards.Where(a => a.ColumnId == column.Id).OrderBy(a => a.Position).Select(a => a.Id).ToListAsync();
  }

  // ----- create -----

  [Fact]
  public async Task Create_AppendsToTheColumnWithVersionOneAndNotifies()
  {
    AddCard(position: 3000);
    var handler = new CreateCardCommandHandler(_fixture.Db, _fixture.CurrentUser, _fixture.Positions, _fixture.Activity, _fixture.Notifier);

    var result = await handler.Handle(new CreateCardCommand(_world.Todo.Id, "  Write docs ", " text ", null), default);

    result.Value.Should().BeEquivalentTo(new { Title = "Write docs", Description = "text", Position = 4024d, Version = 1, ColumnId = _world.Todo.Id });
    var stored = await ReloadAsync(result.Value.Id);
    stored.CreatedById.Should().Be(_world.Member.Id);
    await _fixture.Notifier.Received(1).PublishAsync(
      Arg.Is<BoardEvent>(a => a is CardCreatedEvent && a.ActorUserId == _world.Member.Id),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public void CreateValidator_ChecksTitleAndDescriptionLengths()
  {
    var validator = new CreateCardCommandValidator();
    var id = Guid.NewGuid();

    validator.Validate(new CreateCardCommand(id, "", null, null)).IsValid.Should().BeFalse();
    validator.Validate(new CreateCardCommand(id, new string('x', 201), null, null)).IsValid.Should().BeFalse();
    validator.Validate(new CreateCardCommand(id, "ok", new string('x', 10_001), null)).IsValid.Should().BeFalse();
    validator.Validate(new CreateCardCommand(id, new string('x', 200), new string('x', 10_000), DateTime.UtcNow)).IsValid.Should().BeTrue();
  }

  [Fact]
  public async Task Create_IsRejectedOnArchivedBoards()
  {
    _world.Board.IsArchived = true;
    _fixture.Db.SaveChanges();
    var handler = new CreateCardCommandHandler(_fixture.Db, _fixture.CurrentUser, _fixture.Positions, _fixture.Activity, _fixture.Notifier);

    var result = await handler.Handle(new CreateCardCommand(_world.Todo.Id, "Nope", null, null), default);

    result.Error!.Code.Should().Be("board.archived");
  }

  // ----- update / optimistic concurrency -----

  private UpdateCardCommandHandler UpdateHandler() =>
    new(_fixture.Db, _fixture.CurrentUser, _fixture.Activity, _fixture.Notifier);

  [Fact]
  public async Task Update_AppliesChangesAndBumpsTheVersion()
  {
    var card = AddCard();
    var due = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    var result = await UpdateHandler().Handle(new UpdateCardCommand(card.Id, 1, "New title", "New text", due), default);

    result.Value.Should().BeEquivalentTo(new { Title = "New title", Description = "New text", DueDate = due, Version = 2 });
    (await ReloadAsync(card.Id)).Version.Should().Be(2);
    await _fixture.Notifier.Received(1).PublishAsync(Arg.Is<BoardEvent>(a => a is CardUpdatedEvent), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Update_WithAStaleVersionIsAConflictAndChangesNothing()
  {
    var card = AddCard(title: "Original");
    await UpdateHandler().Handle(new UpdateCardCommand(card.Id, 1, "Alice's edit", null, null), default);
    _fixture.ClearTracking();

    var stale = await UpdateHandler().Handle(new UpdateCardCommand(card.Id, 1, "Bob's edit", null, null), default);

    stale.Error!.Should().BeEquivalentTo(new { Type = ErrorType.Conflict, Code = "card.version_conflict" });
    var stored = await ReloadAsync(card.Id);
    stored.Title.Should().Be("Alice's edit");
    stored.Version.Should().Be(2);
    await _fixture.Notifier.Received(1).PublishAsync(Arg.Any<BoardEvent>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Update_DetectsAConcurrentWriteThatHappensAfterTheVersionCheck()
  {
    // Another request commits between this handler's read and its SaveChanges. The version check alone cannot see it;
    // the EF concurrency token turns it into DbUpdateConcurrencyException (mapped to 409 by the middleware).
    var card = AddCard();
    var loaded = await _fixture.Db.Cards.SingleAsync(a => a.Id == card.Id);
    await _fixture.Db.Database.ExecuteSqlRawAsync("UPDATE Cards SET Version = Version + 1, Title = 'Other' WHERE Id = {0}", card.Id);

    loaded.Title = "Mine";
    loaded.Version++;
    var save = () => _fixture.Db.SaveChangesAsync();

    await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
  }

  [Fact]
  public void UpdateValidator_RequiresAVersionAndATitle()
  {
    var validator = new UpdateCardCommandValidator();

    validator.Validate(new UpdateCardCommand(Guid.NewGuid(), 0, "t", null, null)).IsValid.Should().BeFalse();
    validator.Validate(new UpdateCardCommand(Guid.NewGuid(), 1, "", null, null)).IsValid.Should().BeFalse();
    validator.Validate(new UpdateCardCommand(Guid.NewGuid(), 1, "t", null, null)).IsValid.Should().BeTrue();
  }

  // ----- move -----

  [Fact]
  public async Task Move_ReordersWithinTheColumnBetweenTwoNeighbours()
  {
    var a = AddCard(position: 1024, title: "A");
    var b = AddCard(position: 2048, title: "B");
    var c = AddCard(position: 3072, title: "C");

    var result = await Move(c, _world.Todo, a.Id, b.Id);

    result.Value.Should().BeEquivalentTo(new { Position = 1536d, Version = 2, Rebalanced = false, ColumnId = _world.Todo.Id });
    (await ColumnOrderAsync(_world.Todo)).Should().Equal(a.Id, c.Id, b.Id);
  }

  [Fact]
  public async Task Move_ToAnotherColumnAppendsWhenNoNeighboursAreGiven()
  {
    var card = AddCard();
    var existing = AddCard(_world.Doing, 5000);

    var result = await Move(card, _world.Doing, null, null);

    result.Value.Position.Should().Be(6024);
    (await ColumnOrderAsync(_world.Doing)).Should().Equal(existing.Id, card.Id);
    (await ColumnOrderAsync(_world.Todo)).Should().BeEmpty();
    await _fixture.Notifier.Received(1).PublishAsync(
      Arg.Is<BoardEvent>(e => e is CardMovedEvent && ((CardMovedEvent)e).FromColumnId == _world.Todo.Id && ((CardMovedEvent)e).ToColumnId == _world.Doing.Id),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Move_IntoAnEmptyColumn()
  {
    var card = AddCard();

    var result = await Move(card, _world.Doing, null, null);

    result.Value.Position.Should().Be(1024);
  }

  [Fact]
  public async Task Move_WithOnlyOneNeighbourDerivesTheOtherFromTheCurrentOrder()
  {
    var a = AddCard(_world.Doing, 1000, "A");
    var b = AddCard(_world.Doing, 2000, "B");
    var moving = AddCard(_world.Todo, 1024, "Moving");

    var afterA = await Move(moving, _world.Doing, a.Id, null);
    afterA.Value.Position.Should().Be(1500, "between A and B");

    _fixture.ClearTracking();
    var beforeA = await Move(await ReloadAsync(moving.Id), _world.Doing, null, a.Id);
    beforeA.Value.Position.Should().Be(1000 - 1024, "above the first card");

    (await ColumnOrderAsync(_world.Doing)).Should().Equal(moving.Id, a.Id, b.Id);
  }

  [Fact]
  public async Task Move_ToTheLastSlotAfterTheLastCard()
  {
    var a = AddCard(_world.Doing, 1000, "A");
    var moving = AddCard(_world.Todo);

    var result = await Move(moving, _world.Doing, a.Id, null);

    result.Value.Position.Should().Be(2024);
  }

  [Fact]
  public async Task Move_RebalancesTheTargetColumnWhenTheGapIsExhausted()
  {
    var a = AddCard(position: 1, title: "A");
    var b = AddCard(position: 1 + 1e-8, title: "B");
    var moving = AddCard(_world.Doing, 1024, "Moving");

    var result = await Move(moving, _world.Todo, a.Id, b.Id);

    result.Value.Rebalanced.Should().BeTrue();
    (await ColumnOrderAsync(_world.Todo)).Should().Equal(a.Id, moving.Id, b.Id);
    _fixture.ClearTracking();
    var positions = await _fixture.Db.Cards.Where(x => x.ColumnId == _world.Todo.Id).OrderBy(x => x.Position).Select(x => x.Position).ToListAsync();
    positions.Zip(positions.Skip(1), (x, y) => y - x).Should().OnlyContain(gap => gap > 1);
    await _fixture.Notifier.Received(1).PublishAsync(
      Arg.Is<BoardEvent>(e => e is CardMovedEvent && ((CardMovedEvent)e).Rebalanced),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Move_WithAStaleVersionIsAConflict()
  {
    var card = AddCard();

    var result = await Move(card, _world.Doing, null, null, version: 7);

    result.Error!.Code.Should().Be("card.version_conflict");
    (await ReloadAsync(card.Id)).ColumnId.Should().Be(_world.Todo.Id);
  }

  [Fact]
  public async Task Move_RejectsAColumnOfAnotherBoard()
  {
    var card = AddCard();
    var otherBoard = _fixture.Add(new BoardBuilder(_world.Workspace).Build());
    var foreign = _fixture.Add(new ColumnBuilder(otherBoard, "Foreign").Build());

    var result = await Move(card, foreign, null, null);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.Validation, Code = "card.target_column_invalid" });
    (await ReloadAsync(card.Id)).ColumnId.Should().Be(_world.Todo.Id);
  }

  [Fact]
  public async Task Move_RejectsNeighboursOutsideTheTargetColumn()
  {
    var card = AddCard();
    var elsewhere = AddCard(_world.Doing, 1000);

    var result = await Move(card, _world.Todo, elsewhere.Id, null);

    result.Error!.Code.Should().Be("position.neighbor_not_found");
    (await ReloadAsync(card.Id)).Version.Should().Be(1);
  }

  [Fact]
  public async Task Move_RejectsNeighboursInTheWrongOrder()
  {
    AddCard(position: 1000, title: "A");
    var a = AddCard(position: 1000, title: "A2");
    var b = AddCard(position: 2000, title: "B");
    var moving = AddCard(_world.Doing);

    var result = await Move(moving, _world.Todo, b.Id, a.Id);

    result.Error!.Code.Should().Be("position.neighbors_out_of_order");
  }

  [Fact]
  public async Task Move_IsRejectedOnArchivedBoards()
  {
    var card = AddCard();
    _world.Board.IsArchived = true;
    _fixture.Db.SaveChanges();

    var result = await Move(card, _world.Doing, null, null);

    result.Error!.Code.Should().Be("board.archived");
  }

  [Fact]
  public async Task Move_LogsReorderAndColumnChangesDifferently()
  {
    var card = AddCard();
    var sibling = AddCard(position: 2048);

    await Move(card, _world.Todo, sibling.Id, null);
    _fixture.ClearTracking();
    await Move(await ReloadAsync(card.Id), _world.Doing, null, null);

    var messages = await _fixture.Db.ActivityLogs.OrderBy(a => a.CreatedAt).Select(a => a.Message).ToListAsync();
    messages.Should().Contain(a => a.StartsWith("Reordered")).And.Contain(a => a.Contains("from \"To do\" to \"Doing\""));
  }

  // ----- delete -----

  [Fact]
  public async Task Delete_SoftDeletesAndNotifies()
  {
    var card = AddCard();
    var handler = new DeleteCardCommandHandler(_fixture.Db, _fixture.CurrentUser, _fixture.Time, _fixture.Activity, _fixture.Notifier);

    var result = await handler.Handle(new DeleteCardCommand(card.Id), default);

    result.IsSuccess.Should().BeTrue();
    _fixture.ClearTracking();
    (await _fixture.Db.Cards.AnyAsync()).Should().BeFalse();
    await _fixture.Notifier.Received(1).PublishAsync(
      Arg.Is<BoardEvent>(e => e is CardDeletedEvent && ((CardDeletedEvent)e).CardId == card.Id && ((CardDeletedEvent)e).ColumnId == _world.Todo.Id),
      Arg.Any<CancellationToken>());
  }

  // ----- labels -----

  private SetCardLabelsCommandHandler LabelsHandler() => new(_fixture.Db, _fixture.CurrentUser, _fixture.Activity, _fixture.Notifier);

  [Fact]
  public async Task SetLabels_ReplacesTheSetAndBumpsTheVersion()
  {
    var card = _fixture.Add(new CardBuilder(_world.Board, _world.Todo, _world.Owner).Labeled(_world.Bug).Build());

    var result = await LabelsHandler().Handle(new SetCardLabelsCommand(card.Id, 1, [_world.Feature.Id]), default);

    result.Value.LabelIds.Should().Equal(_world.Feature.Id);
    result.Value.Version.Should().Be(2);
  }

  [Fact]
  public async Task SetLabels_IsIdempotentForDuplicatesAndCanClear()
  {
    var card = AddCard();

    var both = await LabelsHandler().Handle(new SetCardLabelsCommand(card.Id, 1, [_world.Bug.Id, _world.Bug.Id, _world.Feature.Id]), default);
    var none = await LabelsHandler().Handle(new SetCardLabelsCommand(card.Id, 2, []), default);

    both.Value.LabelIds.Should().BeEquivalentTo([_world.Bug.Id, _world.Feature.Id]);
    none.Value.LabelIds.Should().BeEmpty();
  }

  [Fact]
  public async Task SetLabels_RejectsLabelsOfOtherBoardsAndStaleVersions()
  {
    var card = AddCard();
    var other = _fixture.Add(new BoardBuilder(_world.Workspace).Build());
    var foreign = _fixture.Add(new Label { BoardId = other.Id, Name = "Foreign" });

    var wrongBoard = await LabelsHandler().Handle(new SetCardLabelsCommand(card.Id, 1, [foreign.Id]), default);
    var stale = await LabelsHandler().Handle(new SetCardLabelsCommand(card.Id, 5, [_world.Bug.Id]), default);

    wrongBoard.Error!.Code.Should().Be("card.label_not_on_board");
    stale.Error!.Code.Should().Be("card.version_conflict");
  }

  // ----- assignees -----

  private SetCardAssigneesCommandHandler AssigneesHandler() =>
    new(_fixture.Db, _fixture.CurrentUser, _fixture.Authorization, _fixture.Time, _fixture.Activity, _fixture.Notifier);

  private async Task<Result<CardDto>> Assign(Card card, int version, params Guid[] userIds)
  {
    _fixture.Authorization.WorkspaceId = _world.Workspace.Id;
    return await AssigneesHandler().Handle(new SetCardAssigneesCommand(card.Id, version, userIds), default);
  }

  [Fact]
  public async Task SetAssignees_AddsAndRemovesAndNotifiesNewAssigneesExceptTheActor()
  {
    var card = AddCard();

    var result = await Assign(card, 1, _world.Admin.Id, _world.Member.Id);

    result.Value.AssigneeIds.Should().BeEquivalentTo([_world.Admin.Id, _world.Member.Id]);
    result.Value.Version.Should().Be(2);
    var notifications = await _fixture.Db.Notifications.ToListAsync();
    notifications.Should().ContainSingle("assigning yourself does not notify").Which.Should().BeEquivalentTo(new
    {
      UserId = _world.Admin.Id,
      ActorId = _world.Member.Id,
      Type = NotificationType.Assigned,
      CardId = card.Id,
      BoardId = _world.Board.Id,
      IsRead = false
    });
    notifications[0].Message.Should().Contain("Member").And.Contain(card.Title);

    _fixture.ClearTracking();
    var reduced = await Assign(await ReloadAsync(card.Id), 2, _world.Member.Id);
    reduced.Value.AssigneeIds.Should().Equal(_world.Member.Id);
    (await _fixture.Db.Notifications.CountAsync()).Should().Be(1, "removing or keeping assignees does not notify again");
  }

  [Fact]
  public async Task SetAssignees_RejectsPeopleOutsideTheWorkspace()
  {
    var card = AddCard();

    var result = await Assign(card, 1, _world.Outsider.Id);

    result.Error!.Code.Should().Be("card.assignee_not_member");
    (await _fixture.Db.Notifications.AnyAsync()).Should().BeFalse();
  }

  [Fact]
  public async Task SetAssignees_WithAStaleVersionIsAConflict()
  {
    var card = AddCard();

    (await Assign(card, 9, _world.Admin.Id)).Error!.Code.Should().Be("card.version_conflict");
  }

  [Fact]
  public async Task Get_ReturnsTheDto()
  {
    var card = _fixture.Add(new CardBuilder(_world.Board, _world.Todo, _world.Owner).Labeled(_world.Bug).AssignedTo(_world.Admin).Build());

    var result = await new GetCardQueryHandler(_fixture.Db).Handle(new GetCardQuery(card.Id), default);

    result.Value.Should().BeEquivalentTo(new { Id = card.Id, LabelIds = new[] { _world.Bug.Id }, AssigneeIds = new[] { _world.Admin.Id }, CommentCount = 0 });
  }
}
