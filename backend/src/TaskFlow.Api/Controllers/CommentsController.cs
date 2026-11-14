using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Activity;
using TaskFlow.Application.Features.Comments;
using TaskFlow.Application.Features.Notifications;

namespace TaskFlow.Api.Controllers;

[Route(ApiRoutes.Base)]
public sealed class CommentsController(ISender sender) : ApiControllerBase
{
  [HttpGet("cards/{cardId:guid}/comments")]
  public async Task<ActionResult<PagedResult<CommentDto>>> List(Guid cardId, [FromQuery] PageParameters paging) =>
    Respond(await sender.Send(new ListCommentsQuery(cardId, paging.Page, paging.PageSize)));

  [HttpPost("cards/{cardId:guid}/comments")]
  [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
  public async Task<ActionResult<CommentDto>> Add(Guid cardId, CommentRequest request) =>
    RespondCreated(await sender.Send(new AddCommentCommand(cardId, request.Body)));

  [HttpPut("comments/{commentId:guid}")]
  public async Task<ActionResult<CommentDto>> Update(Guid commentId, CommentRequest request) =>
    Respond(await sender.Send(new UpdateCommentCommand(commentId, request.Body)));

  [HttpDelete("comments/{commentId:guid}")]
  public async Task<ActionResult> Delete(Guid commentId) =>
    Respond(await sender.Send(new DeleteCommentCommand(commentId)));
}

[Route(ApiRoutes.Base)]
public sealed class ActivityController(ISender sender) : ApiControllerBase
{
  [HttpGet("boards/{boardId:guid}/activity")]
  public async Task<ActionResult<PagedResult<ActivityDto>>> ForBoard(Guid boardId, [FromQuery] PageParameters paging) =>
    Respond(await sender.Send(new GetBoardActivityQuery(boardId, paging.Page, paging.PageSize)));

  [HttpGet("cards/{cardId:guid}/activity")]
  public async Task<ActionResult<PagedResult<ActivityDto>>> ForCard(Guid cardId, [FromQuery] PageParameters paging) =>
    Respond(await sender.Send(new GetCardActivityQuery(cardId, paging.Page, paging.PageSize)));
}

[Route(ApiRoutes.Base + "/notifications")]
public sealed class NotificationsController(ISender sender) : ApiControllerBase
{
  [HttpGet]
  public async Task<ActionResult<PagedResult<NotificationDto>>> List([FromQuery] NotificationParameters query) =>
    Respond(await sender.Send(new ListNotificationsQuery(query.UnreadOnly, query.Page, query.PageSize)));

  [HttpGet("unread-count")]
  public async Task<ActionResult<UnreadCountDto>> UnreadCount() => Respond(await sender.Send(new GetUnreadCountQuery()));

  [HttpPost("{notificationId:guid}/read")]
  public async Task<ActionResult> MarkRead(Guid notificationId) =>
    Respond(await sender.Send(new MarkNotificationReadCommand(notificationId)));

  [HttpPost("read-all")]
  public async Task<ActionResult> MarkAllRead() => Respond(await sender.Send(new MarkAllNotificationsReadCommand()));
}
