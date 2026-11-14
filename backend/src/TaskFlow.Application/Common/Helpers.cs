using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common;

/// <summary>Adds activity log rows to the current unit of work (the caller saves).</summary>
public sealed class ActivityRecorder(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
  public void Record(Guid boardId, Guid? cardId, ActivityType type, string message) =>
    db.ActivityLogs.Add(new ActivityLog
    {
      BoardId = boardId,
      CardId = cardId,
      UserId = currentUser.UserId,
      Type = type,
      Message = message,
      CreatedAt = timeProvider.GetUtcNow().UtcDateTime
    });
}

public static class CardQueries
{
  /// <summary>Projection shared by single-card reads and the search endpoint (translatable to SQL).</summary>
  public static readonly Expression<Func<Card, CardDto>> ToDto = a => new CardDto(
    a.Id,
    a.BoardId,
    a.ColumnId,
    a.Title,
    a.Description,
    a.Position,
    a.DueDate,
    a.Version,
    a.Labels.Select(b => b.LabelId).ToList(),
    a.Assignees.Select(b => b.UserId).ToList(),
    a.Comments.Count(),
    a.CreatedAt,
    a.UpdatedAt);

  public static Task<CardDto> GetDtoAsync(IApplicationDbContext db, Guid cardId, CancellationToken cancellationToken) =>
    db.Cards.AsNoTracking().Where(a => a.Id == cardId).Select(ToDto).FirstAsync(cancellationToken);

  public static Error VersionConflict() =>
    Error.Conflict("card.version_conflict", "The card was modified by someone else. Reload and try again.");
}

public static class CommentQueries
{
  public static readonly Expression<Func<CardComment, CommentDto>> ToDto = a => new CommentDto(
    a.Id,
    a.CardId,
    a.AuthorId,
    a.Author.DisplayName,
    a.Body,
    a.CreatedAt,
    a.EditedAt);
}

public static class BoardGuard
{
  public static Error Archived() =>
    Error.Conflict("board.archived", "This board is archived. Restore it before making changes.");

  /// <summary>Archived boards are read-only. Returns the error to hand back, or null when edits are allowed.</summary>
  public static async Task<Error?> CheckEditableAsync(IApplicationDbContext db, Guid boardId, CancellationToken cancellationToken)
  {
    var archived = await db.Boards.AsNoTracking().AnyAsync(a => a.Id == boardId && a.IsArchived, cancellationToken);
    return archived ? Archived() : null;
  }
}
