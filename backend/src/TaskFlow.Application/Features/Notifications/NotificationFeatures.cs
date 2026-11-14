using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Application.Features.Notifications;

public static class NotificationErrors
{
  // Someone else's notification looks exactly like a missing one.
  public static Error NotFound() => Error.NotFound("notification.not_found", "The notification was not found.");
}

// ---------- List ----------

public sealed record ListNotificationsQuery(bool UnreadOnly, int Page, int PageSize) : IRequest<Result<PagedResult<NotificationDto>>>;

public sealed class ListNotificationsQueryValidator : AbstractValidator<ListNotificationsQuery>
{
  public ListNotificationsQueryValidator()
  {
    RuleFor(a => a.Page).GreaterThanOrEqualTo(1);
    RuleFor(a => a.PageSize).InclusiveBetween(1, PagedResultExtensions.MaxPageSize);
  }
}

public sealed class ListNotificationsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
  : IRequestHandler<ListNotificationsQuery, Result<PagedResult<NotificationDto>>>
{
  public async Task<Result<PagedResult<NotificationDto>>> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
  {
    var query = db.Notifications.AsNoTracking().Where(a => a.UserId == currentUser.UserId);
    if (request.UnreadOnly)
    {
      query = query.Where(a => a.IsRead == false);
    }

    return await query
      .OrderByDescending(a => a.CreatedAt)
      .ThenByDescending(a => a.Id)
      .Select(a => new NotificationDto(a.Id, a.Type, a.Message, a.BoardId, a.CardId, a.IsRead, a.CreatedAt))
      .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
  }
}

// ---------- Unread count ----------

public sealed record GetUnreadCountQuery : IRequest<Result<UnreadCountDto>>;

public sealed record UnreadCountDto(int Count);

public sealed class GetUnreadCountQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
  : IRequestHandler<GetUnreadCountQuery, Result<UnreadCountDto>>
{
  public async Task<Result<UnreadCountDto>> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken) =>
    new UnreadCountDto(await db.Notifications.CountAsync(a => a.UserId == currentUser.UserId && a.IsRead == false, cancellationToken));
}

// ---------- Mark read ----------

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result>;

public sealed class MarkNotificationReadCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
  : IRequestHandler<MarkNotificationReadCommand, Result>
{
  public async Task<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
  {
    var notification = await db.Notifications
      .FirstOrDefaultAsync(a => a.Id == request.NotificationId && a.UserId == currentUser.UserId, cancellationToken);
    if (notification is null)
    {
      return NotificationErrors.NotFound();
    }

    notification.IsRead = true;
    await db.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}

public sealed record MarkAllNotificationsReadCommand : IRequest<Result>;

public sealed class MarkAllNotificationsReadCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
  : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
  public async Task<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
  {
    var unread = await db.Notifications
      .Where(a => a.UserId == currentUser.UserId && a.IsRead == false)
      .ToListAsync(cancellationToken);
    foreach (var notification in unread)
    {
      notification.IsRead = true;
    }

    await db.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
