using FluentAssertions;
using TaskFlow.Application.Common;
using TaskFlow.Application.Features.Activity;
using TaskFlow.Application.Features.Notifications;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Tests.Features;

public sealed class ActivityAndNotificationTests : IDisposable
{
  private readonly DbFixture _fixture = new();
  private readonly World _world;

  public ActivityAndNotificationTests()
  {
    _world = _fixture.CreateWorld();
    _fixture.ActAs(_world.Member, WorkspaceRole.Member);
  }

  public void Dispose() => _fixture.Dispose();

  private ActivityLog AddActivity(Guid? cardId, int minutesAgo, string message) =>
    _fixture.Add(new ActivityLog
    {
      BoardId = _world.Board.Id,
      CardId = cardId,
      UserId = _world.Owner.Id,
      Type = ActivityType.CardUpdated,
      Message = message,
      CreatedAt = _fixture.Time.GetUtcNow().UtcDateTime.AddMinutes(-minutesAgo)
    });

  private Notification AddNotification(User user, string message, int minutesAgo = 0, bool read = false) =>
    _fixture.Add(new Notification
    {
      UserId = user.Id,
      Type = NotificationType.Mentioned,
      Message = message,
      BoardId = _world.Board.Id,
      IsRead = read,
      CreatedAt = _fixture.Time.GetUtcNow().UtcDateTime.AddMinutes(-minutesAgo)
    });

  // ----- activity -----

  [Fact]
  public async Task CardActivity_IsNewestFirstAndOnlyForThatCard()
  {
    var one = Guid.NewGuid();
    var two = Guid.NewGuid();
    AddActivity(one, 30, "oldest");
    AddActivity(one, 10, "newest");
    AddActivity(two, 20, "other card");
    AddActivity(null, 5, "board level");

    var page = await new GetCardActivityQueryHandler(_fixture.Db).Handle(new GetCardActivityQuery(one, 1, 10), default);

    page.Value.Items.Select(a => a.Message).Should().Equal("newest", "oldest");
    page.Value.Items[0].UserDisplayName.Should().Be("Owner");
  }

  [Fact]
  public async Task BoardActivity_IncludesEverythingOnTheBoardAndPages()
  {
    AddActivity(Guid.NewGuid(), 3, "a");
    AddActivity(null, 2, "b");
    AddActivity(Guid.NewGuid(), 1, "c");

    var first = await new GetBoardActivityQueryHandler(_fixture.Db).Handle(new GetBoardActivityQuery(_world.Board.Id, 1, 2), default);
    var second = await new GetBoardActivityQueryHandler(_fixture.Db).Handle(new GetBoardActivityQuery(_world.Board.Id, 2, 2), default);

    first.Value.Items.Select(a => a.Message).Should().Equal("c", "b");
    second.Value.Items.Select(a => a.Message).Should().Equal("a");
    first.Value.TotalCount.Should().Be(3);
  }

  [Theory]
  [InlineData(0, 10)]
  [InlineData(1, 0)]
  [InlineData(1, 201)]
  public void ActivityValidators_RejectBadPaging(int page, int pageSize)
  {
    new GetBoardActivityQueryValidator().Validate(new GetBoardActivityQuery(Guid.NewGuid(), page, pageSize)).IsValid.Should().BeFalse();
    new GetCardActivityQueryValidator().Validate(new GetCardActivityQuery(Guid.NewGuid(), page, pageSize)).IsValid.Should().BeFalse();
  }

  // ----- notifications -----

  [Fact]
  public async Task List_ReturnsOnlyMyNotificationsNewestFirst_AndCanFilterUnread()
  {
    AddNotification(_world.Member, "old", minutesAgo: 10, read: true);
    AddNotification(_world.Member, "new", minutesAgo: 1);
    AddNotification(_world.Admin, "not mine");
    var handler = new ListNotificationsQueryHandler(_fixture.Db, _fixture.CurrentUser);

    var all = await handler.Handle(new ListNotificationsQuery(false, 1, 20), default);
    var unread = await handler.Handle(new ListNotificationsQuery(true, 1, 20), default);

    all.Value.Items.Select(a => a.Message).Should().Equal("new", "old");
    unread.Value.Items.Select(a => a.Message).Should().Equal("new");
  }

  [Fact]
  public async Task UnreadCount_CountsOnlyMyUnreadNotifications()
  {
    AddNotification(_world.Member, "a");
    AddNotification(_world.Member, "b");
    AddNotification(_world.Member, "c", read: true);
    AddNotification(_world.Admin, "d");

    var result = await new GetUnreadCountQueryHandler(_fixture.Db, _fixture.CurrentUser).Handle(new GetUnreadCountQuery(), default);

    result.Value.Count.Should().Be(2);
  }

  [Fact]
  public async Task MarkRead_MarksOnlyThatNotification()
  {
    var target = AddNotification(_world.Member, "a");
    var other = AddNotification(_world.Member, "b");

    var result = await new MarkNotificationReadCommandHandler(_fixture.Db, _fixture.CurrentUser)
      .Handle(new MarkNotificationReadCommand(target.Id), default);

    result.IsSuccess.Should().BeTrue();
    _fixture.ClearTracking();
    _fixture.Db.Notifications.Single(a => a.Id == target.Id).IsRead.Should().BeTrue();
    _fixture.Db.Notifications.Single(a => a.Id == other.Id).IsRead.Should().BeFalse();
  }

  [Fact]
  public async Task MarkRead_TreatsSomeoneElsesNotificationAsMissing()
  {
    var theirs = AddNotification(_world.Admin, "secret");

    var result = await new MarkNotificationReadCommandHandler(_fixture.Db, _fixture.CurrentUser)
      .Handle(new MarkNotificationReadCommand(theirs.Id), default);

    result.Error!.Should().BeEquivalentTo(new { Type = ErrorType.NotFound, Code = "notification.not_found" });
    _fixture.ClearTracking();
    _fixture.Db.Notifications.Single().IsRead.Should().BeFalse();
  }

  [Fact]
  public async Task MarkAllRead_OnlyTouchesMyNotifications()
  {
    AddNotification(_world.Member, "a");
    AddNotification(_world.Member, "b");
    AddNotification(_world.Admin, "c");

    var result = await new MarkAllNotificationsReadCommandHandler(_fixture.Db, _fixture.CurrentUser)
      .Handle(new MarkAllNotificationsReadCommand(), default);

    result.IsSuccess.Should().BeTrue();
    _fixture.ClearTracking();
    _fixture.Db.Notifications.Where(a => a.UserId == _world.Member.Id).All(a => a.IsRead).Should().BeTrue();
    _fixture.Db.Notifications.Single(a => a.UserId == _world.Admin.Id).IsRead.Should().BeFalse();
  }
}
