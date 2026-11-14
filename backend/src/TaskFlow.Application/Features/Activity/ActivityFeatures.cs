using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Activity;

internal static class ActivityQueries
{
  public static async Task<PagedResult<ActivityDto>> PageAsync(
    IQueryable<Domain.Entities.ActivityLog> query,
    int page,
    int pageSize,
    CancellationToken cancellationToken) =>
    await query
      .OrderByDescending(a => a.CreatedAt)
      .ThenByDescending(a => a.Id)
      .Select(a => new ActivityDto(a.Id, a.BoardId, a.CardId, a.UserId, a.User.DisplayName, a.Type, a.Message, a.CreatedAt))
      .ToPagedResultAsync(page, pageSize, cancellationToken);
}

public abstract class ActivityPagingValidator<T> : AbstractValidator<T> where T : IPagedRequest
{
  public ActivityPagingValidator()
  {
    RuleFor(a => a.Page).GreaterThanOrEqualTo(1);
    RuleFor(a => a.PageSize).InclusiveBetween(1, PagedResultExtensions.MaxPageSize);
  }
}

public interface IPagedRequest
{
  int Page { get; }

  int PageSize { get; }
}

// ---------- Board ----------

public sealed record GetBoardActivityQuery(Guid BoardId, int Page, int PageSize)
  : IRequest<Result<PagedResult<ActivityDto>>>, IAuthorizedRequest, IPagedRequest
{
  public ResourceScope Scope => ResourceScope.Board(BoardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class GetBoardActivityQueryValidator : ActivityPagingValidator<GetBoardActivityQuery>;

public sealed class GetBoardActivityQueryHandler(IApplicationDbContext db)
  : IRequestHandler<GetBoardActivityQuery, Result<PagedResult<ActivityDto>>>
{
  public async Task<Result<PagedResult<ActivityDto>>> Handle(GetBoardActivityQuery request, CancellationToken cancellationToken) =>
    await ActivityQueries.PageAsync(db.ActivityLogs.AsNoTracking().Where(a => a.BoardId == request.BoardId), request.Page, request.PageSize, cancellationToken);
}

// ---------- Card ----------

public sealed record GetCardActivityQuery(Guid CardId, int Page, int PageSize)
  : IRequest<Result<PagedResult<ActivityDto>>>, IAuthorizedRequest, IPagedRequest
{
  public ResourceScope Scope => ResourceScope.Card(CardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class GetCardActivityQueryValidator : ActivityPagingValidator<GetCardActivityQuery>;

public sealed class GetCardActivityQueryHandler(IApplicationDbContext db)
  : IRequestHandler<GetCardActivityQuery, Result<PagedResult<ActivityDto>>>
{
  public async Task<Result<PagedResult<ActivityDto>>> Handle(GetCardActivityQuery request, CancellationToken cancellationToken) =>
    await ActivityQueries.PageAsync(db.ActivityLogs.AsNoTracking().Where(a => a.CardId == request.CardId), request.Page, request.PageSize, cancellationToken);
}
