using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TaskFlow.Application.Common;
using TaskFlow.Application.Events;
using TaskFlow.Application.Features.Comments;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Features;

public sealed class CommentHandlerTests : IDisposable
{
  private readonly DbFixture _fixture = new();
  private readonly World _world;
  private readonly Card _card;

  public CommentHandlerTests()
  {
    _world = _fixture.CreateWorld();
    _fixture.Authorization.WorkspaceId = _world.Workspace.Id;
    _card = _fixture.Add(new CardBuilder(_world.Board, _world.Todo, _world.Owner).Titled("Ship it").Build());
    ActAs(_world.Member, WorkspaceRole.Member);
  }

  public void Dispose() => _fixture.Dispose();

  private void ActAs(User user, WorkspaceRole role) => _fixture.ActAs(user, role);

  private AddCommentCommandHandler AddHandler() =>
    new(_fixture.Db, _fixture.CurrentUser, _fixture.Authorization, _fixture.Time, _fixture.Activity, _fixture.Notifier);

  private UpdateCommentCommandHandler UpdateHandler() => new(_fixture.Db, _fixture.CurrentUser, _fixture.Time);

  private DeleteCommentCommandHandler DeleteHandler() =>
    new(_fixture.Db, _fixture.CurrentUser, _fixture.Authorization, _fixture.Time, _fixture.Notifier);

  private CardComment AddComment(User author, string body = "text") =>
    _fixture.Add(new CardComment { CardId = _card.Id, AuthorId = author.Id, Body = body, CreatedAt = _fixture.Time.GetUtcNow().UtcDateTime });

  private async Task<List<Notification>> NotificationsAsync()
  {
    _fixture.ClearTracking();
    return await _fixture.Db.Notifications.AsNoTracking().ToListAsync();
  }

  // ----- add -----

  [Fact]
  public async Task Add_StoresTrimmedBodyWithAuthor_RecordsActivityAndPublishes()
  {
    var result = await AddHandler().Handle(new AddCommentCommand(_card.Id, "  Looks good  "), default);

    result.Value.Should().BeEquivalentTo(new { Body = "Looks good", AuthorId = _world.Member.Id, AuthorDisplayName = "Member", CardId = _card.Id });
    _fixture.ClearTracking();
    (await _fixture.Db.ActivityLogs.SingleAsync(a => a.Type == ActivityType.CommentAdded)).CardId.Should().Be(_card.Id);
    await _fixture.Notifier.Received(1).PublishAsync(
      Arg.Is<BoardEvent>(a => a is CommentAddedEvent && a.ActorUserId == _world.Member.Id),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Add_NotifiesMentionedWorkspaceMembers()
  {
    var result = await AddHandler().Handle(new AddCommentCommand(_card.Id, $"@{_world.Admin.Handle} please review"), default);

    result.IsSuccess.Should().BeTrue();
    var notification = (await NotificationsAsync()).Should().ContainSingle().Subject;
    notification.Should().BeEquivalentTo(new
    {
      UserId = _world.Admin.Id,
      ActorId = _world.Member.Id,
      Type = NotificationType.Mentioned,
      CardId = _card.Id,
      BoardId = _world.Board.Id,
      IsRead = false
    });
    notification.Message.Should().Contain("Member").And.Contain("Ship it");
  }

  [Fact]
  public async Task Add_DoesNotNotifyYourselfOutsidersOrUnknownHandles_AndNotifiesEachPersonOnce()
  {
    var body = $"@{_world.Member.Handle} @{_world.Outsider.Handle} @nobody @{_world.Owner.Handle} @{_world.Owner.Handle}";

    await AddHandler().Handle(new AddCommentCommand(_card.Id, body), default);

    (await NotificationsAsync()).Select(a => a.UserId).Should().Equal(_world.Owner.Id);
  }

  [Fact]
  public async Task Add_MatchesHandlesIgnoringCaseAndTrailingPunctuation()
  {
    await AddHandler().Handle(new AddCommentCommand(_card.Id, $"Thanks @{_world.Admin.Handle.ToUpperInvariant()}."), default);

    (await NotificationsAsync()).Should().ContainSingle(a => a.UserId == _world.Admin.Id);
  }

  [Fact]
  public async Task Add_IsRejectedOnArchivedBoards()
  {
    _world.Board.IsArchived = true;
    _fixture.Db.SaveChanges();

    var result = await AddHandler().Handle(new AddCommentCommand(_card.Id, "late"), default);

    result.Error!.Code.Should().Be("board.archived");
    (await _fixture.Db.CardComments.CountAsync()).Should().Be(0);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void Validator_RejectsEmptyBodies(string body) =>
    new AddCommentCommandValidator().Validate(new AddCommentCommand(Guid.NewGuid(), body)).IsValid.Should().BeFalse();

  [Fact]
  public void Validator_RejectsTooLongBodies()
  {
    var validator = new AddCommentCommandValidator();

    validator.Validate(new AddCommentCommand(Guid.NewGuid(), new string('a', 5000))).IsValid.Should().BeTrue();
    validator.Validate(new AddCommentCommand(Guid.NewGuid(), new string('a', 5001))).IsValid.Should().BeFalse();
  }

  // ----- edit -----

  [Fact]
  public async Task Update_LetsTheAuthorEditAndMarksTheCommentEdited()
  {
    var comment = AddComment(_world.Member, "old");
    _fixture.Time.Advance(TimeSpan.FromMinutes(5));

    var result = await UpdateHandler().Handle(new UpdateCommentCommand(comment.Id, " new "), default);

    result.Value.Should().BeEquivalentTo(new { Body = "new", EditedAt = _fixture.Time.GetUtcNow().UtcDateTime });
  }

  [Theory]
  [InlineData(WorkspaceRole.Admin)]
  [InlineData(WorkspaceRole.Owner)]
  public async Task Update_IsAuthorOnly_EvenForAdminsAndOwners(WorkspaceRole role)
  {
    var comment = AddComment(_world.Member, "mine");
    ActAs(role == WorkspaceRole.Admin ? _world.Admin : _world.Owner, role);

    var result = await UpdateHandler().Handle(new UpdateCommentCommand(comment.Id, "hijacked"), default);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.Forbidden, Code = "comment.not_author" });
    _fixture.ClearTracking();
    (await _fixture.Db.CardComments.SingleAsync()).Body.Should().Be("mine");
  }

  [Fact]
  public async Task Update_IsRejectedOnArchivedBoards()
  {
    var comment = AddComment(_world.Member);
    _world.Board.IsArchived = true;
    _fixture.Db.SaveChanges();

    (await UpdateHandler().Handle(new UpdateCommentCommand(comment.Id, "x"), default)).Error!.Code.Should().Be("board.archived");
  }

  // ----- delete -----

  [Fact]
  public async Task Delete_LetsTheAuthorRemoveTheirCommentAndPublishes()
  {
    var comment = AddComment(_world.Member);

    var result = await DeleteHandler().Handle(new DeleteCommentCommand(comment.Id), default);

    result.IsSuccess.Should().BeTrue();
    _fixture.ClearTracking();
    (await _fixture.Db.CardComments.CountAsync()).Should().Be(0, "soft-deleted comments are filtered out");
    await _fixture.Notifier.Received(1).PublishAsync(
      Arg.Is<BoardEvent>(a => a is CommentDeletedEvent && ((CommentDeletedEvent)a).CommentId == comment.Id),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Delete_LetsAnAdminRemoveSomeoneElsesComment()
  {
    var comment = AddComment(_world.Member);
    ActAs(_world.Admin, WorkspaceRole.Admin);

    (await DeleteHandler().Handle(new DeleteCommentCommand(comment.Id), default)).IsSuccess.Should().BeTrue();
  }

  [Fact]
  public async Task Delete_ForbidsAPlainMemberFromRemovingSomeoneElsesComment()
  {
    var comment = AddComment(_world.Owner);

    var result = await DeleteHandler().Handle(new DeleteCommentCommand(comment.Id), default);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.Forbidden, Code = "comment.delete_forbidden" });
    _fixture.ClearTracking();
    (await _fixture.Db.CardComments.CountAsync()).Should().Be(1);
  }

  // ----- list -----

  [Fact]
  public async Task List_ReturnsOldestFirstWithPaging()
  {
    for (var i = 1; i <= 5; i++)
    {
      var comment = AddComment(_world.Member, $"c{i}");
      comment.CreatedAt = _fixture.Time.GetUtcNow().UtcDateTime.AddMinutes(i);
      _fixture.Db.SaveChanges();
    }

    var page = await new ListCommentsQueryHandler(_fixture.Db).Handle(new ListCommentsQuery(_card.Id, 2, 2), default);

    page.Value.Items.Select(a => a.Body).Should().Equal("c3", "c4");
    page.Value.TotalCount.Should().Be(5);
    page.Value.TotalPages.Should().Be(3);
  }
}
