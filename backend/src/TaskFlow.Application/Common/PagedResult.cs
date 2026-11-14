using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
  public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class PagedResultExtensions
{
  public const int MaxPageSize = 200;

  public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
    this IQueryable<T> query,
    int page,
    int pageSize,
    CancellationToken cancellationToken)
  {
    var totalCount = await query.CountAsync(cancellationToken);
    var items = await query
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .ToListAsync(cancellationToken);

    return new PagedResult<T>(items, page, pageSize, totalCount);
  }
}
