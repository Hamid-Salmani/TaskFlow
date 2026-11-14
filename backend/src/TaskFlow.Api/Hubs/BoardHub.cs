using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.Api.Services;
using TaskFlow.Application.Common;
using TaskFlow.Application.Events;

namespace TaskFlow.Api.Hubs;

[Authorize]
public sealed class BoardHub(IAccessChecker accessChecker) : Hub
{
  public const string Path = "/hubs/board";

  public static string GroupName(Guid boardId) => $"board:{boardId}";

  public async Task JoinBoard(Guid boardId)
  {
    var userId = Context.User?.GetUserId();
    if (userId is null || await accessChecker.IsBoardMemberAsync(boardId, userId.Value, Context.ConnectionAborted) == false)
    {
      // Same answer for "missing" and "not a member": existence is not leaked.
      throw new HubException("Board not found.");
    }

    await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId), Context.ConnectionAborted);
  }

  public Task LeaveBoard(Guid boardId) =>
    Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(boardId), Context.ConnectionAborted);
}

public sealed class SignalRBoardNotifier(IHubContext<BoardHub> hubContext, ILogger<SignalRBoardNotifier> logger) : IBoardNotifier
{
  public async Task PublishAsync(BoardEvent boardEvent, CancellationToken cancellationToken = default)
  {
    try
    {
      // Passing the runtime type matters: the hub protocol serializes the argument's concrete record.
      await hubContext.Clients
        .Group(BoardHub.GroupName(boardEvent.BoardId))
        .SendAsync(boardEvent.Name, (object)boardEvent, cancellationToken);
    }
    catch (Exception exception)
    {
      // Realtime is best effort: the HTTP request already succeeded and clients can always refetch.
      logger.LogWarning(exception, "Could not publish {EventName} for board {BoardId}", boardEvent.Name, boardEvent.BoardId);
    }
  }
}
