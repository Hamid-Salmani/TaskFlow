using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Events;
using TaskFlow.Application.Ordering;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Columns;

public static class ColumnMapping
{
  public static ColumnDto ToDto(Column column) => new(column.Id, column.BoardId, column.Title, column.Position);
}

// ---------- Create ----------

public sealed record CreateColumnCommand(Guid BoardId, string Title) : IRequest<Result<ColumnDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Board(BoardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class CreateColumnCommandValidator : AbstractValidator<CreateColumnCommand>
{
  public CreateColumnCommandValidator() => RuleFor(a => a.Title).NotEmpty().MaximumLength(100);
}

public sealed class CreateColumnCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  IPositionService positions,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<CreateColumnCommand, Result<ColumnDto>>
{
  public async Task<Result<ColumnDto>> Handle(CreateColumnCommand request, CancellationToken cancellationToken)
  {
    if (await BoardGuard.CheckEditableAsync(db, request.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    var last = await db.Columns
      .Where(a => a.BoardId == request.BoardId)
      .OrderByDescending(a => a.Position)
      .Select(a => (double?)a.Position)
      .FirstOrDefaultAsync(cancellationToken);

    var column = new Column
    {
      BoardId = request.BoardId,
      Title = request.Title.Trim(),
      Position = positions.Between(last, null).Position
    };
    db.Columns.Add(column);
    activity.Record(request.BoardId, null, ActivityType.ColumnCreated, $"Added column \"{column.Title}\"");
    await db.SaveChangesAsync(cancellationToken);

    await notifier.PublishAsync(new ColumnsChangedEvent(request.BoardId, currentUser.UserId), cancellationToken);
    return ColumnMapping.ToDto(column);
  }
}

// ---------- Rename ----------

public sealed record RenameColumnCommand(Guid ColumnId, string Title) : IRequest<Result<ColumnDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Column(ColumnId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class RenameColumnCommandValidator : AbstractValidator<RenameColumnCommand>
{
  public RenameColumnCommandValidator() => RuleFor(a => a.Title).NotEmpty().MaximumLength(100);
}

public sealed class RenameColumnCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<RenameColumnCommand, Result<ColumnDto>>
{
  public async Task<Result<ColumnDto>> Handle(RenameColumnCommand request, CancellationToken cancellationToken)
  {
    var column = await db.Columns.FirstAsync(a => a.Id == request.ColumnId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, column.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    var previous = column.Title;
    column.Title = request.Title.Trim();
    activity.Record(column.BoardId, null, ActivityType.ColumnRenamed, $"Renamed column \"{previous}\" to \"{column.Title}\"");
    await db.SaveChangesAsync(cancellationToken);

    await notifier.PublishAsync(new ColumnsChangedEvent(column.BoardId, currentUser.UserId), cancellationToken);
    return ColumnMapping.ToDto(column);
  }
}

// ---------- Move (reorder) ----------

public sealed record MoveColumnCommand(Guid ColumnId, Guid? AfterId, Guid? BeforeId)
  : IRequest<Result<MoveColumnResultDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Column(ColumnId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class MoveColumnCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  IPositionService positions,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<MoveColumnCommand, Result<MoveColumnResultDto>>
{
  public async Task<Result<MoveColumnResultDto>> Handle(MoveColumnCommand request, CancellationToken cancellationToken)
  {
    var column = await db.Columns.FirstAsync(a => a.Id == request.ColumnId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, column.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    var siblings = await db.Columns
      .Where(a => a.BoardId == column.BoardId && a.Id != column.Id)
      .OrderBy(a => a.Position)
      .ThenBy(a => a.Id)
      .ToListAsync(cancellationToken);

    var placement = PositionPlanner.Plan(positions, siblings, request.AfterId, request.BeforeId);
    if (placement.IsFailure)
    {
      return placement.Error!;
    }

    column.Position = placement.Value.Position;
    activity.Record(column.BoardId, null, ActivityType.ColumnMoved, $"Moved column \"{column.Title}\"");
    await db.SaveChangesAsync(cancellationToken);

    await notifier.PublishAsync(new ColumnsChangedEvent(column.BoardId, currentUser.UserId), cancellationToken);
    return new MoveColumnResultDto(column.Id, column.Position, placement.Value.Rebalanced);
  }
}

// ---------- Delete ----------

/// <summary>Deleting a column deletes its cards, so it needs the admin role.</summary>
public sealed record DeleteColumnCommand(Guid ColumnId) : IRequest<Result>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Column(ColumnId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Admin;
}

public sealed class DeleteColumnCommandHandler(
  IApplicationDbContext db,
  ICurrentUser currentUser,
  TimeProvider timeProvider,
  ActivityRecorder activity,
  IBoardNotifier notifier) : IRequestHandler<DeleteColumnCommand, Result>
{
  public async Task<Result> Handle(DeleteColumnCommand request, CancellationToken cancellationToken)
  {
    var column = await db.Columns.FirstAsync(a => a.Id == request.ColumnId, cancellationToken);
    if (await BoardGuard.CheckEditableAsync(db, column.BoardId, cancellationToken) is { } archived)
    {
      return archived;
    }

    var now = timeProvider.GetUtcNow().UtcDateTime;
    var cards = await db.Cards.Where(a => a.ColumnId == column.Id).ToListAsync(cancellationToken);
    foreach (var card in cards)
    {
      card.SoftDelete(now);
    }

    column.SoftDelete(now);
    activity.Record(column.BoardId, null, ActivityType.ColumnDeleted, $"Deleted column \"{column.Title}\" with {cards.Count} card(s)");
    await db.SaveChangesAsync(cancellationToken);

    await notifier.PublishAsync(new ColumnsChangedEvent(column.BoardId, currentUser.UserId), cancellationToken);
    return Result.Success();
  }
}
