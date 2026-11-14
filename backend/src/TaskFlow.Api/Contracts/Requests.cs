#nullable enable
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.Contracts;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record CreateWorkspaceRequest(string Name, string? Description);

public sealed record UpdateWorkspaceRequest(string Name, string? Description);

public sealed record InviteMemberRequest(string Email, WorkspaceRole Role);

public sealed record ChangeMemberRoleRequest(WorkspaceRole Role);

public sealed record CreateBoardRequest(string Name, string? Description);

public sealed record UpdateBoardRequest(string Name, string? Description);

public sealed record ArchiveBoardRequest(bool Archived);

public sealed record CreateColumnRequest(string Title);

public sealed record UpdateColumnRequest(string Title);

/// <summary>Target slot expressed as neighbour ids. See NOTES.md ("Position format").</summary>
public sealed record MoveColumnRequest(Guid? AfterId, Guid? BeforeId);

public sealed record CreateCardRequest(string Title, string? Description, DateTime? DueDate);

public sealed record UpdateCardRequest(int ExpectedVersion, string Title, string? Description, DateTime? DueDate);

public sealed record MoveCardRequest(int ExpectedVersion, Guid TargetColumnId, Guid? AfterId, Guid? BeforeId);

public sealed record SetCardLabelsRequest(int ExpectedVersion, List<Guid> LabelIds);

public sealed record SetCardAssigneesRequest(int ExpectedVersion, List<Guid> UserIds);

public sealed record LabelRequest(string Name, string Color);

public sealed record CommentRequest(string Body);

public sealed class CardSearchParameters
{
  [FromQuery(Name = "labelId")] public Guid? LabelId { get; set; }

  [FromQuery(Name = "assigneeId")] public Guid? AssigneeId { get; set; }

  [FromQuery(Name = "hasDueDate")] public bool? HasDueDate { get; set; }

  [FromQuery(Name = "dueFrom")] public DateTime? DueFrom { get; set; }

  [FromQuery(Name = "dueTo")] public DateTime? DueTo { get; set; }

  [FromQuery(Name = "q")] public string? Q { get; set; }

  [FromQuery(Name = "sortBy")] public CardSortField SortBy { get; set; } = CardSortField.Position;

  [FromQuery(Name = "sortDirection")] public SortDirection SortDirection { get; set; } = SortDirection.Asc;

  [FromQuery(Name = "page")] public int Page { get; set; } = 1;

  [FromQuery(Name = "pageSize")] public int PageSize { get; set; } = 50;
}

public sealed class PageParameters
{
  [FromQuery(Name = "page")] public int Page { get; set; } = 1;

  [FromQuery(Name = "pageSize")] public int PageSize { get; set; } = 20;
}

public sealed class NotificationParameters
{
  [FromQuery(Name = "unreadOnly")] public bool UnreadOnly { get; set; }

  [FromQuery(Name = "page")] public int Page { get; set; } = 1;

  [FromQuery(Name = "pageSize")] public int PageSize { get; set; } = 20;
}
