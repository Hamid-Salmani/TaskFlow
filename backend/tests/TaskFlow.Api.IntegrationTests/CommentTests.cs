using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.IntegrationTests.Support;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Notifications;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.IntegrationTests;

public sealed class CommentTests(TaskFlowFactory factory) : IClassFixture<TaskFlowFactory>
{
  private const string Api = "/api/v1";

  private sealed record Setup(TestUser Owner, TestUser Admin, TestUser Member, TestUser Outsider, BoardDetailDto Board, CardDto Card);

  private async Task<Setup> CreateSetupAsync()
  {
    var owner = await factory.RegisterUserAsync("Owner");
    var admin = await factory.RegisterUserAsync("Admin");
    var member = await factory.RegisterUserAsync("Member");
    var outsider = await factory.RegisterUserAsync("Outsider");
    var workspace = await owner.CreateWorkspaceAsync();
    await owner.AddMemberAsync(workspace.Id, admin, WorkspaceRole.Admin);
    await owner.AddMemberAsync(workspace.Id, member);
    var board = await owner.CreateBoardAsync(workspace.Id);
    var card = await owner.CreateCardAsync(board.Columns[0].Id, "Discuss me");
    return new Setup(owner, admin, member, outsider, board, card);
  }

  private static async Task<CommentDto> CommentAsync(TestUser user, Guid cardId, string body)
  {
    var response = await user.Client.PostJsonAsync($"{Api}/cards/{cardId}/comments", new CommentRequest(body));
    response.StatusCode.Should().Be(HttpStatusCode.Created);
    return await response.ReadAsAsync<CommentDto>();
  }

  private static async Task<PagedResult<NotificationDto>> NotificationsAsync(TestUser user, string query = "") =>
    await (await user.Client.GetAsync($"{Api}/notifications{query}")).ReadAsAsync<PagedResult<NotificationDto>>();

  [Fact]
  public async Task AddingAComment_ShowsUpInTheListAndTheCardsCommentCount()
  {
    var s = await CreateSetupAsync();

    await CommentAsync(s.Member, s.Card.Id, "first");
    await CommentAsync(s.Owner, s.Card.Id, "second");

    var list = await (await s.Admin.Client.GetAsync($"{Api}/cards/{s.Card.Id}/comments")).ReadAsAsync<PagedResult<CommentDto>>();
    list.Items.Select(a => a.Body).Should().Equal("first", "second");
    list.Items[0].AuthorDisplayName.Should().Be("Member");
    var card = await (await s.Owner.Client.GetAsync($"{Api}/cards/{s.Card.Id}")).ReadAsAsync<CardDto>();
    card.CommentCount.Should().Be(2);
  }

  [Fact]
  public async Task NonMembersCannotReadOrWriteComments()
  {
    var s = await CreateSetupAsync();
    var comment = await CommentAsync(s.Member, s.Card.Id, "private");

    (await s.Outsider.Client.GetAsync($"{Api}/cards/{s.Card.Id}/comments")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await s.Outsider.Client.PostJsonAsync($"{Api}/cards/{s.Card.Id}/comments", new CommentRequest("hi"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await s.Outsider.Client.PutJsonAsync($"{Api}/comments/{comment.Id}", new CommentRequest("hi"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await s.Outsider.Client.DeleteAsync($"{Api}/comments/{comment.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await s.Outsider.Client.GetAsync($"{Api}/cards/{s.Card.Id}/activity")).StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task AnEmptyCommentIsABadRequestWithFieldErrors()
  {
    var s = await CreateSetupAsync();

    var response = await s.Member.Client.PostJsonAsync($"{Api}/cards/{s.Card.Id}/comments", new CommentRequest(" "));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    (await response.ReadProblemAsync()).Errors.Should().ContainKey("body");
  }

  [Fact]
  public async Task OnlyTheAuthorCanEdit_AndTheEditIsMarked()
  {
    var s = await CreateSetupAsync();
    var comment = await CommentAsync(s.Member, s.Card.Id, "typo");

    var byAdmin = await s.Admin.Client.PutJsonAsync($"{Api}/comments/{comment.Id}", new CommentRequest("hijack"));
    var byAuthor = await s.Member.Client.PutJsonAsync($"{Api}/comments/{comment.Id}", new CommentRequest("fixed"));

    byAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    (await byAdmin.ReadProblemAsync()).Code.Should().Be("comment.not_author");
    byAuthor.StatusCode.Should().Be(HttpStatusCode.OK);
    var edited = await byAuthor.ReadAsAsync<CommentDto>();
    edited.Body.Should().Be("fixed");
    edited.EditedAt.Should().NotBeNull();
  }

  [Fact]
  public async Task AuthorAndAdminCanDelete_ButAnotherMemberCannot()
  {
    var s = await CreateSetupAsync();
    var other = await factory.RegisterUserAsync("Other");
    await s.Owner.AddMemberAsync((await s.Owner.Client.GetAsync($"{Api}/boards/{s.Board.Id}")).ReadAsAsync<BoardDetailDto>().Result.WorkspaceId, other);
    var comment = await CommentAsync(s.Member, s.Card.Id, "to delete");

    (await other.Client.DeleteAsync($"{Api}/comments/{comment.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    (await s.Admin.Client.DeleteAsync($"{Api}/comments/{comment.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

    var mine = await CommentAsync(s.Member, s.Card.Id, "mine");
    (await s.Member.Client.DeleteAsync($"{Api}/comments/{mine.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    var list = await (await s.Owner.Client.GetAsync($"{Api}/cards/{s.Card.Id}/comments")).ReadAsAsync<PagedResult<CommentDto>>();
    list.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task MentioningAMember_CreatesAnUnreadNotificationThatCanBeMarkedRead()
  {
    var s = await CreateSetupAsync();

    await CommentAsync(s.Member, s.Card.Id, $"Can you look, @{s.Admin.Handle}? cc @{s.Outsider.Handle}");

    var adminInbox = await NotificationsAsync(s.Admin);
    var notification = adminInbox.Items.Should().ContainSingle().Subject;
    notification.Type.Should().Be(NotificationType.Mentioned);
    notification.CardId.Should().Be(s.Card.Id);
    notification.IsRead.Should().BeFalse();
    (await NotificationsAsync(s.Outsider)).Items.Should().BeEmpty("only workspace members can be mentioned");
    (await NotificationsAsync(s.Member)).Items.Should().BeEmpty("the author is not notified");

    (await s.Admin.Client.GetAsync($"{Api}/notifications/unread-count")).ReadAsAsync<UnreadCountDto>().Result.Count.Should().Be(1);
    (await s.Admin.Client.PostAsync($"{Api}/notifications/{notification.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    (await NotificationsAsync(s.Admin, "?unreadOnly=true")).Items.Should().BeEmpty();
    (await NotificationsAsync(s.Admin)).Items.Single().IsRead.Should().BeTrue();
  }

  [Fact]
  public async Task AssigningSomeoneNotifiesThem_AndNobodyCanReadOrMarkSomeoneElsesNotifications()
  {
    var s = await CreateSetupAsync();
    var response = await s.Owner.Client.PutJsonAsync($"{Api}/cards/{s.Card.Id}/assignees", new SetCardAssigneesRequest(s.Card.Version, [s.Member.Id]));
    response.EnsureSuccessStatusCode();

    var memberInbox = await NotificationsAsync(s.Member);
    memberInbox.Items.Should().ContainSingle(a => a.Type == NotificationType.Assigned);
    (await NotificationsAsync(s.Admin)).Items.Should().BeEmpty();

    var theirs = memberInbox.Items.Single();
    (await s.Admin.Client.PostAsync($"{Api}/notifications/{theirs.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await s.Member.Client.PostAsync($"{Api}/notifications/read-all", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    (await NotificationsAsync(s.Member, "?unreadOnly=true")).Items.Should().BeEmpty();
  }

  [Fact]
  public async Task NotificationsRequireAuthentication()
  {
    using var anonymous = factory.CreateClient();

    (await anonymous.GetAsync($"{Api}/notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task CardAndBoardActivity_RecordWhatHappenedNewestFirst()
  {
    var s = await CreateSetupAsync();
    await CommentAsync(s.Member, s.Card.Id, "hello");

    var cardActivity = await (await s.Member.Client.GetAsync($"{Api}/cards/{s.Card.Id}/activity")).ReadAsAsync<PagedResult<ActivityDto>>();
    var boardActivity = await (await s.Member.Client.GetAsync($"{Api}/boards/{s.Board.Id}/activity?pageSize=50")).ReadAsAsync<PagedResult<ActivityDto>>();

    cardActivity.Items.Select(a => a.Type).Should().Equal(ActivityType.CommentAdded, ActivityType.CardCreated);
    cardActivity.Items[0].UserDisplayName.Should().Be("Member");
    boardActivity.Items.Should().Contain(a => a.Type == ActivityType.BoardCreated).And.Contain(a => a.Type == ActivityType.CommentAdded);
  }

  [Fact]
  public async Task CommentEventsAreBroadcast_WithoutLeakingToNonMembersOfTheBoard()
  {
    var s = await CreateSetupAsync();
    await using var hub = await HubClient.ConnectAsync(factory, s.Admin);
    await hub.Connection.InvokeAsync("JoinBoard", s.Board.Id);

    var comment = await CommentAsync(s.Member, s.Card.Id, "live");
    var added = await hub.NextAsync();
    added.Name.Should().Be("CommentAdded");
    added.Payload.GetProperty("comment").GetProperty("id").GetGuid().Should().Be(comment.Id);
    added.Payload.GetProperty("actorUserId").GetGuid().Should().Be(s.Member.Id);

    (await s.Member.Client.DeleteAsync($"{Api}/comments/{comment.Id}")).EnsureSuccessStatusCode();
    var deleted = await hub.NextAsync();
    deleted.Name.Should().Be("CommentDeleted");
    deleted.Payload.GetProperty("commentId").GetGuid().Should().Be(comment.Id);
  }
}
