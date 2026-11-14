using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Features.Cards;

/// <summary>Filters, sorts and pages the cards of a board. The Kanban view uses it to load the whole board.</summary>
public sealed record SearchCardsQuery(
  Guid BoardId,
  Guid? LabelId,
  Guid? AssigneeId,
  bool? HasDueDate,
  DateTime? DueFrom,
  DateTime? DueTo,
  string? Text,
  CardSortField SortBy,
  SortDirection SortDirection,
  int Page,
  int PageSize) : IRequest<Result<PagedResult<CardDto>>>, IAuthorizedRequest
{
  public ResourceScope Scope => ResourceScope.Board(BoardId);

  public WorkspaceRole MinimumRole => WorkspaceRole.Member;
}

public sealed class SearchCardsQueryValidator : AbstractValidator<SearchCardsQuery>
{
  public SearchCardsQueryValidator()
  {
    RuleFor(a => a.Page).GreaterThanOrEqualTo(1);
    RuleFor(a => a.PageSize).InclusiveBetween(1, PagedResultExtensions.MaxPageSize);
    RuleFor(a => a.Text).MaximumLength(200);
    RuleFor(a => a.SortBy).IsInEnum();
    RuleFor(a => a.SortDirection).IsInEnum();
    RuleFor(a => a.DueTo)
      .GreaterThanOrEqualTo(a => a.DueFrom!.Value)
      .When(a => a.DueFrom is not null && a.DueTo is not null)
      .WithMessage("'dueTo' must not be before 'dueFrom'.");
  }
}

public sealed class SearchCardsQueryHandler(IApplicationDbContext db)
  : IRequestHandler<SearchCardsQuery, Result<PagedResult<CardDto>>>
{
  public async Task<Result<PagedResult<CardDto>>> Handle(SearchCardsQuery request, CancellationToken cancellationToken)
  {
    var query = db.Cards.AsNoTracking().Where(a => a.BoardId == request.BoardId);

    if (request.LabelId is { } labelId)
    {
      query = query.Where(a => a.Labels.Any(b => b.LabelId == labelId));
    }

    if (request.AssigneeId is { } assigneeId)
    {
      query = query.Where(a => a.Assignees.Any(b => b.UserId == assigneeId));
    }

    if (request.HasDueDate is { } hasDueDate)
    {
      query = hasDueDate ? query.Where(a => a.DueDate != null) : query.Where(a => a.DueDate == null);
    }

    if (request.DueFrom is { } dueFrom)
    {
      query = query.Where(a => a.DueDate >= dueFrom);
    }

    if (request.DueTo is { } dueTo)
    {
      query = query.Where(a => a.DueDate <= dueTo);
    }

    if (string.IsNullOrWhiteSpace(request.Text) == false)
    {
      var pattern = $"%{EscapeLike(request.Text.Trim())}%";
      query = query.Where(a =>
        EF.Functions.Like(a.Title, pattern, "\\") ||
        (a.Description != null && EF.Functions.Like(a.Description, pattern, "\\")));
    }

    var descending = request.SortDirection == SortDirection.Desc;
    var ordered = request.SortBy switch
    {
      // Cards without a due date always go last, whichever direction was asked for.
      CardSortField.DueDate => (descending
          ? query.OrderBy(a => a.DueDate == null).ThenByDescending(a => a.DueDate)
          : query.OrderBy(a => a.DueDate == null).ThenBy(a => a.DueDate)),
      CardSortField.CreatedAt => descending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
      CardSortField.Title => descending ? query.OrderByDescending(a => a.Title) : query.OrderBy(a => a.Title),
      _ => descending ? query.OrderByDescending(a => a.Position) : query.OrderBy(a => a.Position)
    };

    // Id as the final tie-breaker keeps pages stable when the sort key has duplicates.
    return await ordered
      .ThenBy(a => a.Id)
      .Select(CardQueries.ToDto)
      .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
  }

  private static string EscapeLike(string text) =>
    text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
