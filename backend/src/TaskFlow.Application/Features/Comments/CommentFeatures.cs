using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Events;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Comments;

public static class CommentErrors
{
  public static Error NotAuthor() =>
    Error.Forbidden("comment.not_author", "Only the author can edit this comment.");

  public static Error NotAuthorOrAdmin() =>
    Error.Forbidden("comment.delete_forbidden", "Only the author or a workspace admin can delete this comment.");
}

internal static class CommentRules
{
  public const int MaxLength = 5000;

  public static void AddBodyRule<T>(IRuleBuilderInitial<T, string> rule) => rule.NotEmpty().MaximumLength(MaxLength);
}

internal static class CommentQueries
{
  public static IQueryable<CommentDto> Project(IQueryable<CardComment> query) =>
    query.Select(a => new CommentDto(a.Id, a.CardId, a.AuthorId, a.Author.DisplayName, a.Body, a.CreatedAt, a.EditedAt));
}

// ---------- List ----------

public sealed record ListCommentsQuery(Guid CardId, int Page, int PageSize)
  : IRequest<Result<PagedResult<CommentDto>>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Card(CardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class ListCommentsQueryValidator : AbstractValidator<ListCommentsQuery>
{
  public ListCommentsQueryValidator()
  {
    RuleFor(a => a.Page).GreaterThanOrEqualTo(1);
    RuleFor(a => a.PageSize).InclusiveBetween(1, PagedResultExtensions.MaxPageSize);
  }
}

public sealed class ListCommentsQueryHandler(IApplicationDbContext db)
  : IRequestHandler<ListCommentsQuery, Result<PagedResult<CommentDto>>>
{
  public async Task<Result<PagedResult<CommentDto>>> Handle(ListCommentsQuery request, CancellationToken cancellationToken) =>
    await CommentQueries
      .Project(db.CardComments.AsNoTracking().Where(a => a.CardId == request.CardId).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id))
      .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
}

// ---------- Add ----------

public sealed record AddCommentCommand(Guid CardId, string Body) : IRequest<Result<CommentDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Card(CardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
  public AddCommentCommandValidator() => CommentRules.AddBodyRule(RuleFor(a => a.Body));
}

public sealed class AddCommentCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  IAuthorizationContext authorization,
  TimeProvider timeProvider,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<AddCommentCommand, Result<CommentDto>>
{
  public async Task<Result<CommentDto>> Handle(AddCommentCommand request, CancellationToken cancellationToken)
  {
    var card = await db.Cards.AsNoTracking().FirstAsync(a => a.Id == request.CardId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, card.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    var now = timeProvider.GetUtcNow().UtcDateTime;
    var body = request.Body.Trim();
    var comment = new CardComment { CardId = card.Id, AuthorId = currentUser.UserId, Body = body, CreatedAt = now };
    db.CardComments.Add(comment);

    var authorName = await db.Users
      .Where(a => a.Id == currentUser.UserId)
      .Select(a => a.DisplayName)
      .FirstAsync(cancellationToken);

    // Only members of this workspace can be mentioned, and nobody is notified about mentioning themselves.
    var handles = MentionParser.Parse(body);
    if (handles.Count > 0)
    {
      var mentioned = await db.WorkspaceMembers
        .Where(a => a.WorkspaceId == authorization.WorkspaceId && a.UserId != currentUser.UserId)
        .Where(a => handles.Contains(a.User.Handle.ToLower()))
        .Select(a => a.UserId)
        .ToListAsync(cancellationToken);

      foreach (var userId in mentioned)
      {
        db.Notifications.Add(new Notification
        {
          UserId = userId,
          ActorId = currentUser.UserId,
          Type = NotificationType.Mentioned,
          Message = $"{authorName} mentioned you on \"{card.Title}\"",
          BoardId = card.BoardId,
          CardId = card.Id,
          CreatedAt = now
        });
      }
    }

    activity.Record(card.BoardId, card.Id, ActivityType.CommentAdded, $"Commented on \"{card.Title}\"");
    await db.SaveChangesAsync(cancellationToken);

    var dto = new CommentDto(comment.Id, card.Id, comment.AuthorId, authorName, comment.Body, comment.CreatedAt, null);
    await notifier.PublishAsync(new CommentAddedEvent(card.BoardId, currentUser.UserId, card.Id, dto), cancellationToken);
    return dto;
  }
}

// ---------- Edit ----------

public sealed record UpdateCommentCommand(Guid CommentId, string Body) : IRequest<Result<CommentDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Comment(CommentId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class UpdateCommentCommandValidator : AbstractValidator<UpdateCommentCommand>
{
  public UpdateCommentCommandValidator() => CommentRules.AddBodyRule(RuleFor(a => a.Body));
}

public sealed class UpdateCommentCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
  : IRequestHandler<UpdateCommentCommand, Result<CommentDto>>
{
  public async Task<Result<CommentDto>> Handle(UpdateCommentCommand request, CancellationToken cancellationToken)
  {
    var comment = await db.CardComments.Include(a => a.Author).Include(a => a.Card).FirstAsync(a => a.Id == request.CommentId, cancellationToken);

    // Not even admins edit other people's words.
    if (comment.AuthorId != currentUser.UserId)
    {
      return CommentErrors.NotAuthor();
    }

    if (await BoardGuard.CheckEditableAsync(db, comment.Card.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    var now = timeProvider.GetUtcNow().UtcDateTime;
    comment.Body = request.Body.Trim();
    comment.EditedAt = now;
    comment.UpdatedAt = now;
    await db.SaveChangesAsync(cancellationToken);

    return new CommentDto(comment.Id, comment.CardId, comment.AuthorId, comment.Author.DisplayName, comment.Body, comment.CreatedAt, comment.EditedAt);
  }
}

// ---------- Delete ----------

public sealed record DeleteCommentCommand(Guid CommentId) : IRequest<Result>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Comment(CommentId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class DeleteCommentCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  IAuthorizationContext authorization,
  TimeProvider timeProvider,
  IBoardNotifier notifier) : IRequestHandler<DeleteCommentCommand, Result>
{
  public async Task<Result> Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
  {
    var comment = await db.CardComments.Include(a => a.Card).FirstAsync(a => a.Id == request.CommentId, cancellationToken);

    if (comment.AuthorId != currentUser.UserId && authorization.Role < WorkspaceRole.Admin)
    {
      return CommentErrors.NotAuthorOrAdmin();
    }

    if (await BoardGuard.CheckEditableAsync(db, comment.Card.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    comment.SoftDelete(timeProvider.GetUtcNow().UtcDateTime);
    await db.SaveChangesAsync(cancellationToken);

    await notifier.PublishAsync(
      new CommentDeletedEvent(comment.Card.BoardId, currentUser.UserId, comment.CardId, comment.Id),
      cancellationToken);
    return Result.Success();
  }
}
