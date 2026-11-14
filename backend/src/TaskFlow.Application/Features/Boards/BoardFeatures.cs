using FluentValidation;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Workspaces;
using TaskFlow.Application.Ordering;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Boards;

public static class BoardDefaults
{
  /// <summary>New boards start with a classic three-column flow so they are usable immediately.</summary>
  public static readonly string[] ColumnTitles = ["To do", "In progress", "Done"];
}

// ---------- Create ----------

public sealed record CreateBoardCommand(Guid WorkspaceId, string Name, string? Description)
  : IRequest<Result<BoardSummaryDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class CreateBoardCommandValidator : AbstractValidator<CreateBoardCommand>
{
  public CreateBoardCommandValidator()
  {
    RuleFor(a => a.Name).NotEmpty().MaximumLength(100);
    RuleFor(a => a.Description).MaximumLength(1000);
  }
}

public sealed class CreateBoardCommandHandler(IApplicationDbContext db, IPositionService positions, ActivityRecorder activity)
  : IRequestHandler<CreateBoardCommand, Result<BoardSummaryDto>>
{
  public async Task<Result<BoardSummaryDto>> Handle(CreateBoardCommand request, CancellationToken cancellationToken)
  {
    var board = new Board
    {
      WorkspaceId = request.WorkspaceId,
      Name = request.Name.Trim(),
      Description = request.Description?.Trim()
    };

    var initial = positions.Rebalance(BoardDefaults.ColumnTitles.Length);
    for (var i = 0; i < BoardDefaults.ColumnTitles.Length; i++)
    {
      board.Columns.Add(new Column { Title = BoardDefaults.ColumnTitles[i], Position = initial[i] });
    }

    db.Boards.Add(board);
    activity.Record(board.Id, null, ActivityType.BoardCreated, $"Created board \"{board.Name}\"");
    await db.SaveChangesAsync(cancellationToken);

    return board.Adapt<BoardSummaryDto>();
  }
}

// ---------- List ----------

public sealed record ListBoardsQuery(Guid WorkspaceId, bool IncludeArchived)
  : IRequest<Result<List<BoardSummaryDto>>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Workspace(WorkspaceId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class ListBoardsQueryHandler(IApplicationDbContext db)
  : IRequestHandler<ListBoardsQuery, Result<List<BoardSummaryDto>>>
{
  public async Task<Result<List<BoardSummaryDto>>> Handle(ListBoardsQuery request, CancellationToken cancellationToken) =>
    await db.Boards
      .AsNoTracking()
      .Where(a => a.WorkspaceId == request.WorkspaceId && (request.IncludeArchived || a.IsArchived == false))
      .OrderBy(a => a.IsArchived)
      .ThenBy(a => a.Name)
      .Select(a => new BoardSummaryDto(a.Id, a.WorkspaceId, a.Name, a.Description, a.IsArchived, a.CreatedAt))
      .ToListAsync(cancellationToken);
}

// ---------- Get ----------

public sealed record GetBoardQuery(Guid BoardId) : IRequest<Result<BoardDetailDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Board(BoardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class GetBoardQueryHandler(IApplicationDbContext db, IAuthorizationContext authorization)
  : IRequestHandler<GetBoardQuery, Result<BoardDetailDto>>
{
  public async Task<Result<BoardDetailDto>> Handle(GetBoardQuery request, CancellationToken cancellationToken)
  {
    var board = await db.Boards
      .AsNoTracking()
      .Where(a => a.Id == request.BoardId)
      .Select(a => new { a.Id, a.WorkspaceId, a.Name, a.Description, a.IsArchived })
      .FirstAsync(cancellationToken);

    var columns = await db.Columns
      .AsNoTracking()
      .Where(a => a.BoardId == board.Id)
      .OrderBy(a => a.Position)
      .ThenBy(a => a.Id)
      .Select(a => new ColumnDto(a.Id, a.BoardId, a.Title, a.Position))
      .ToListAsync(cancellationToken);

    var labels = await db.Labels
      .AsNoTracking()
      .Where(a => a.BoardId == board.Id)
      .OrderBy(a => a.Name)
      .Select(a => new LabelDto(a.Id, a.BoardId, a.Name, a.Color))
      .ToListAsync(cancellationToken);

    var members = await db.WorkspaceMembers
      .AsNoTracking()
      .Where(a => a.WorkspaceId == board.WorkspaceId)
      .OrderByRoleThenName()
      .Select(WorkspaceQueries.MemberToDto)
      .ToListAsync(cancellationToken);

    return new BoardDetailDto(
      board.Id,
      board.WorkspaceId,
      board.Name,
      board.Description,
      board.IsArchived,
      authorization.Role,
      columns,
      labels,
      members);
  }
}

// ---------- Update / archive / delete ----------

public sealed record UpdateBoardCommand(Guid BoardId, string Name, string? Description)
  : IRequest<Result<BoardSummaryDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Board(BoardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Admin;
}

public sealed class UpdateBoardCommandValidator : AbstractValidator<UpdateBoardCommand>
{
  public UpdateBoardCommandValidator()
  {
    RuleFor(a => a.Name).NotEmpty().MaximumLength(100);
    RuleFor(a => a.Description).MaximumLength(1000);
  }
}

public sealed class UpdateBoardCommandHandler(IApplicationDbContext db)
  : IRequestHandler<UpdateBoardCommand, Result<BoardSummaryDto>>
{
  public async Task<Result<BoardSummaryDto>> Handle(UpdateBoardCommand request, CancellationToken cancellationToken)
  {
    var board = await db.Boards.FirstAsync(a => a.Id == request.BoardId, cancellationToken);
    board.Name = request.Name.Trim();
    board.Description = request.Description?.Trim();
    await db.SaveChangesAsync(cancellationToken);
    return board.Adapt<BoardSummaryDto>();
  }
}

public sealed record ArchiveBoardCommand(Guid BoardId, bool Archived) : IRequest<Result<BoardSummaryDto>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Board(BoardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Admin;
}

public sealed class ArchiveBoardCommandHandler(IApplicationDbContext db)
  : IRequestHandler<ArchiveBoardCommand, Result<BoardSummaryDto>>
{
  public async Task<Result<BoardSummaryDto>> Handle(ArchiveBoardCommand request, CancellationToken cancellationToken)
  {
    var board = await db.Boards.FirstAsync(a => a.Id == request.BoardId, cancellationToken);
    board.IsArchived = request.Archived;
    await db.SaveChangesAsync(cancellationToken);
    return board.Adapt<BoardSummaryDto>();
  }
}

public sealed record DeleteBoardCommand(Guid BoardId) : IRequest<Result>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Board(BoardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Admin;
}

public sealed class DeleteBoardCommandHandler(IApplicationDbContext db, TimeProvider timeProvider)
  : IRequestHandler<DeleteBoardCommand, Result>
{
  public async Task<Result> Handle(DeleteBoardCommand request, CancellationToken cancellationToken)
  {
    var board = await db.Boards.FirstAsync(a => a.Id == request.BoardId, cancellationToken);
    board.SoftDelete(timeProvider.GetUtcNow().UtcDateTime);
    await db.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}
