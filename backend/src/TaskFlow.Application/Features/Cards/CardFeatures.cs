using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Events;
using TaskFlow.Application.Ordering;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Cards;

public static class CardErrors
{
  public static Error TargetColumnInvalid() =>
    Error.Validation("card.target_column_invalid", "The target column does not exist on this board.");

  public static Error AssigneeNotMember() =>
    Error.Validation("card.assignee_not_member", "Every assignee must be a member of the workspace.");

  public static Error LabelNotOnBoard() =>
    Error.Validation("card.label_not_on_board", "Every label must belong to the card's board.");
}

internal static class CardRules
{
  public static void AddTitleRule<T>(IRuleBuilderInitial<T, string> rule) => rule.NotEmpty().MaximumLength(200);
}

// ---------- Create ----------

public sealed record CreateCardCommand(Guid ColumnId, string Title, string? Description, DateTime? DueDate)
  : IRequest<Result<CardDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Column(ColumnId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class CreateCardCommandValidator : AbstractValidator<CreateCardCommand>
{
  public CreateCardCommandValidator()
  {
    CardRules.AddTitleRule(RuleFor(a => a.Title));
    RuleFor(a => a.Description).MaximumLength(10_000);
  }
}

public sealed class CreateCardCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  IPositionService positions,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<CreateCardCommand, Result<CardDto>>
{
  public async Task<Result<CardDto>> Handle(CreateCardCommand request, CancellationToken cancellationToken)
  {
    var column = await db.Columns.AsNoTracking().FirstAsync(a => a.Id == request.ColumnId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, column.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    var last = await db.Cards
      .Where(a => a.ColumnId == column.Id)
      .OrderByDescending(a => a.Position)
      .Select(a => (double?)a.Position)
      .FirstOrDefaultAsync(cancellationToken);

    var card = new Card
    {
      BoardId = column.BoardId,
      ColumnId = column.Id,
      Title = request.Title.Trim(),
      Description = request.Description?.Trim(),
      DueDate = request.DueDate,
      CreatedById = currentUser.UserId,
      Position = positions.Between(last, null).Position
    };
    db.Cards.Add(card);
    activity.Record(column.BoardId, card.Id, ActivityType.CardCreated, $"Created card \"{card.Title}\" in \"{column.Title}\"");
    await db.SaveChangesAsync(cancellationToken);

    var dto = await CardQueries.GetDtoAsync(db, card.Id, cancellationToken);
    await notifier.PublishAsync(new CardCreatedEvent(card.BoardId, currentUser.UserId, dto), cancellationToken);
    return dto;
  }
}

// ---------- Get ----------

public sealed record GetCardQuery(Guid CardId) : IRequest<Result<CardDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Card(CardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class GetCardQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCardQuery, Result<CardDto>>
{
  public async Task<Result<CardDto>> Handle(GetCardQuery request, CancellationToken cancellationToken) =>
    await CardQueries.GetDtoAsync(db, request.CardId, cancellationToken);
}

// ---------- Update ----------

public sealed record UpdateCardCommand(Guid CardId, int ExpectedVersion, string Title, string? Description, DateTime? DueDate)
  : IRequest<Result<CardDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Card(CardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class UpdateCardCommandValidator : AbstractValidator<UpdateCardCommand>
{
  public UpdateCardCommandValidator()
  {
    RuleFor(a => a.ExpectedVersion).GreaterThanOrEqualTo(1);
    CardRules.AddTitleRule(RuleFor(a => a.Title));
    RuleFor(a => a.Description).MaximumLength(10_000);
  }
}

public sealed class UpdateCardCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<UpdateCardCommand, Result<CardDto>>
{
  public async Task<Result<CardDto>> Handle(UpdateCardCommand request, CancellationToken cancellationToken)
  {
    var card = await db.Cards.FirstAsync(a => a.Id == request.CardId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, card.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    if (card.Version != request.ExpectedVersion)
    {
      return CardQueries.VersionConflict();
    }

    card.Title = request.Title.Trim();
    card.Description = request.Description?.Trim();
    card.DueDate = request.DueDate;
    card.Version++;
    activity.Record(card.BoardId, card.Id, ActivityType.CardUpdated, $"Updated card \"{card.Title}\"");
    await db.SaveChangesAsync(cancellationToken);

    var dto = await CardQueries.GetDtoAsync(db, card.Id, cancellationToken);
    await notifier.PublishAsync(new CardUpdatedEvent(card.BoardId, currentUser.UserId, dto), cancellationToken);
    return dto;
  }
}

// ---------- Move ----------

public sealed record MoveCardCommand(Guid CardId, int ExpectedVersion, Guid TargetColumnId, Guid? AfterId, Guid? BeforeId)
  : IRequest<Result<MoveCardResultDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Card(CardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class MoveCardCommandValidator : AbstractValidator<MoveCardCommand>
{
  public MoveCardCommandValidator()
  {
    RuleFor(a => a.ExpectedVersion).GreaterThanOrEqualTo(1);
    RuleFor(a => a.TargetColumnId).NotEmpty();
  }
}

public sealed class MoveCardCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  IPositionService positions,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<MoveCardCommand, Result<MoveCardResultDto>>
{
  public async Task<Result<MoveCardResultDto>> Handle(MoveCardCommand request, CancellationToken cancellationToken)
  {
    var card = await db.Cards.FirstAsync(a => a.Id == request.CardId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, card.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    if (card.Version != request.ExpectedVersion)
    {
      return CardQueries.VersionConflict();
    }

    // Columns of other boards must be unreachable: moving across boards would bypass the authorization check.
    var titles = await db.Columns
      .AsNoTracking()
      .Where(a => a.BoardId == card.BoardId && (a.Id == request.TargetColumnId || a.Id == card.ColumnId))
      .ToDictionaryAsync(a => a.Id, a => a.Title, cancellationToken);
    if (titles.ContainsKey(request.TargetColumnId) == false)
    {
      return CardErrors.TargetColumnInvalid();
    }

    var siblings = await db.Cards
      .Where(a => a.ColumnId == request.TargetColumnId && a.Id != card.Id)
      .OrderBy(a => a.Position)
      .ThenBy(a => a.Id)
      .ToListAsync(cancellationToken);

    var placement = PositionPlanner.Plan(positions, siblings, request.AfterId, request.BeforeId);
    if (placement.IsFailure)
    {
      return placement.Error!;
    }

    var fromColumnId = card.ColumnId;
    card.ColumnId = request.TargetColumnId;
    card.Position = placement.Value.Position;
    card.Version++;

    var message = fromColumnId == request.TargetColumnId
      ? $"Reordered card \"{card.Title}\" in \"{titles[fromColumnId]}\""
      : $"Moved card \"{card.Title}\" from \"{titles.GetValueOrDefault(fromColumnId, "a deleted column")}\" to \"{titles[request.TargetColumnId]}\"";
    activity.Record(card.BoardId, card.Id, ActivityType.CardMoved, message);
    await db.SaveChangesAsync(cancellationToken);

    await notifier.PublishAsync(
      new CardMovedEvent(
        card.BoardId,
        currentUser.UserId,
        card.Id,
        fromColumnId,
        card.ColumnId,
        card.Position,
        card.Version,
        placement.Value.Rebalanced),
      cancellationToken);

    return new MoveCardResultDto(card.Id, card.ColumnId, card.Position, card.Version, placement.Value.Rebalanced);
  }
}

// ---------- Delete ----------

public sealed record DeleteCardCommand(Guid CardId) : IRequest<Result>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Card(CardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class DeleteCardCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  TimeProvider timeProvider,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<DeleteCardCommand, Result>
{
  public async Task<Result> Handle(DeleteCardCommand request, CancellationToken cancellationToken)
  {
    var card = await db.Cards.FirstAsync(a => a.Id == request.CardId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, card.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    card.SoftDelete(timeProvider.GetUtcNow().UtcDateTime);
    activity.Record(card.BoardId, card.Id, ActivityType.CardDeleted, $"Deleted card \"{card.Title}\"");
    await db.SaveChangesAsync(cancellationToken);

    await notifier.PublishAsync(new CardDeletedEvent(card.BoardId, currentUser.UserId, card.Id, card.ColumnId), cancellationToken);
    return Result.Success();
  }
}

// ---------- Labels of a card ----------

public sealed record SetCardLabelsCommand(Guid CardId, int ExpectedVersion, IReadOnlyList<Guid> LabelIds)
  : IRequest<Result<CardDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Card(CardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class SetCardLabelsCommandValidator : AbstractValidator<SetCardLabelsCommand>
{
  public SetCardLabelsCommandValidator()
  {
    RuleFor(a => a.ExpectedVersion).GreaterThanOrEqualTo(1);
    RuleFor(a => a.LabelIds).NotNull().Must(a => a.Count <= 50).WithMessage("A card can have at most 50 labels.");
  }
}

public sealed class SetCardLabelsCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<SetCardLabelsCommand, Result<CardDto>>
{
  public async Task<Result<CardDto>> Handle(SetCardLabelsCommand request, CancellationToken cancellationToken)
  {
    var card = await db.Cards.Include(a => a.Labels).FirstAsync(a => a.Id == request.CardId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, card.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    if (card.Version != request.ExpectedVersion)
    {
      return CardQueries.VersionConflict();
    }

    var wanted = request.LabelIds.Distinct().ToHashSet();
    var valid = await db.Labels.CountAsync(a => a.BoardId == card.BoardId && wanted.Contains(a.Id), cancellationToken);
    if (valid != wanted.Count)
    {
      return CardErrors.LabelNotOnBoard();
    }

    foreach (var existing in card.Labels.Where(a => wanted.Contains(a.LabelId) == false).ToList())
    {
      card.Labels.Remove(existing);
    }

    var current = card.Labels.Select(a => a.LabelId).ToHashSet();
    foreach (var labelId in wanted.Where(a => current.Contains(a) == false))
    {
      card.Labels.Add(new CardLabel { CardId = card.Id, LabelId = labelId });
    }

    card.Version++;
    activity.Record(card.BoardId, card.Id, ActivityType.CardLabelsChanged, $"Changed labels of \"{card.Title}\"");
    await db.SaveChangesAsync(cancellationToken);

    var dto = await CardQueries.GetDtoAsync(db, card.Id, cancellationToken);
    await notifier.PublishAsync(new CardUpdatedEvent(card.BoardId, currentUser.UserId, dto), cancellationToken);
    return dto;
  }
}

// ---------- Assignees of a card ----------

public sealed record SetCardAssigneesCommand(Guid CardId, int ExpectedVersion, IReadOnlyList<Guid> UserIds)
  : IRequest<Result<CardDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Card(CardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class SetCardAssigneesCommandValidator : AbstractValidator<SetCardAssigneesCommand>
{
  public SetCardAssigneesCommandValidator()
  {
    RuleFor(a => a.ExpectedVersion).GreaterThanOrEqualTo(1);
    RuleFor(a => a.UserIds).NotNull().Must(a => a.Count <= 50).WithMessage("A card can have at most 50 assignees.");
  }
}

public sealed class SetCardAssigneesCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  IAuthorizationContext authorization,
  TimeProvider timeProvider,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<SetCardAssigneesCommand, Result<CardDto>>
{
  public async Task<Result<CardDto>> Handle(SetCardAssigneesCommand request, CancellationToken cancellationToken)
  {
    var card = await db.Cards.Include(a => a.Assignees).FirstAsync(a => a.Id == request.CardId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, card.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    if (card.Version != request.ExpectedVersion)
    {
      return CardQueries.VersionConflict();
    }

    var wanted = request.UserIds.Distinct().ToHashSet();
    var members = await db.WorkspaceMembers.CountAsync(
      a => a.WorkspaceId == authorization.WorkspaceId && wanted.Contains(a.UserId),
      cancellationToken);
    if (members != wanted.Count)
    {
      return CardErrors.AssigneeNotMember();
    }

    foreach (var removed in card.Assignees.Where(a => wanted.Contains(a.UserId) == false).ToList())
    {
      card.Assignees.Remove(removed);
    }

    var now = timeProvider.GetUtcNow().UtcDateTime;
    var already = card.Assignees.Select(a => a.UserId).ToHashSet();
    var added = wanted.Where(a => already.Contains(a) == false).ToList();
    foreach (var userId in added)
    {
      card.Assignees.Add(new CardAssignee { CardId = card.Id, UserId = userId, AssignedAt = now });
    }

    // Assigning yourself is not worth a notification.
    var toNotify = added.Where(a => a != currentUser.UserId).ToList();
    if (toNotify.Count > 0)
    {
      var actorName = await db.Users
        .Where(a => a.Id == currentUser.UserId)
        .Select(a => a.DisplayName)
        .FirstAsync(cancellationToken);
      foreach (var userId in toNotify)
      {
        db.Notifications.Add(new Notification
        {
          UserId = userId,
          ActorId = currentUser.UserId,
          Type = NotificationType.Assigned,
          Message = $"{actorName} assigned you to \"{card.Title}\"",
          BoardId = card.BoardId,
          CardId = card.Id,
          CreatedAt = now
        });
      }
    }

    card.Version++;
    activity.Record(card.BoardId, card.Id, ActivityType.CardAssigned, $"Changed assignees of \"{card.Title}\"");
    await db.SaveChangesAsync(cancellationToken);

    var dto = await CardQueries.GetDtoAsync(db, card.Id, cancellationToken);
    await notifier.PublishAsync(new CardUpdatedEvent(card.BoardId, currentUser.UserId, dto), cancellationToken);
    return dto;
  }
}
