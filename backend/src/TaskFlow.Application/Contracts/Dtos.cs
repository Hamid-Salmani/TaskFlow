using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Contracts;

public sealed record UserDto(Guid Id, string Email, string DisplayName, string Handle);

public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, UserDto User);

public sealed record WorkspaceDto(Guid Id, string Name, string? Description, WorkspaceRole MyRole, int MemberCount, int BoardCount);

public sealed record WorkspaceMemberDto(Guid UserId, string Email, string DisplayName, string Handle, WorkspaceRole Role, DateTime JoinedAt);

public sealed record BoardSummaryDto(Guid Id, Guid WorkspaceId, string Name, string? Description, bool IsArchived, DateTime CreatedAt);

public sealed record ColumnDto(Guid Id, Guid BoardId, string Title, double Position);

public sealed record LabelDto(Guid Id, Guid BoardId, string Name, string Color);

public sealed record BoardDetailDto(
  Guid Id,
  Guid WorkspaceId,
  string Name,
  string? Description,
  bool IsArchived,
  WorkspaceRole MyRole,
  List<ColumnDto> Columns,
  List<LabelDto> Labels,
  List<WorkspaceMemberDto> Members);

public sealed record CardDto(
  Guid Id,
  Guid BoardId,
  Guid ColumnId,
  string Title,
  string? Description,
  double Position,
  DateTime? DueDate,
  int Version,
  List<Guid> LabelIds,
  List<Guid> AssigneeIds,
  int CommentCount,
  DateTime CreatedAt,
  DateTime? UpdatedAt);

public sealed record MoveCardResultDto(Guid Id, Guid ColumnId, double Position, int Version, bool Rebalanced);

public sealed record MoveColumnResultDto(Guid Id, double Position, bool Rebalanced);

public sealed record CommentDto(
  Guid Id,
  Guid CardId,
  Guid AuthorId,
  string AuthorDisplayName,
  string Body,
  DateTime CreatedAt,
  DateTime? EditedAt);

public sealed record ActivityDto(
  Guid Id,
  Guid BoardId,
  Guid? CardId,
  Guid UserId,
  string UserDisplayName,
  ActivityType Type,
  string Message,
  DateTime CreatedAt);

public sealed record NotificationDto(
  Guid Id,
  NotificationType Type,
  string Message,
  Guid BoardId,
  Guid? CardId,
  bool IsRead,
  DateTime CreatedAt);

public enum CardSortField
{
  Position,
  DueDate,
  CreatedAt,
  Title
}

public enum SortDirection
{
  Asc,
  Desc
}
