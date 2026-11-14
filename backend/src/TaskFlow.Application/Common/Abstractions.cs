using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Events;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common;

public interface IApplicationDbContext
{
  DbSet<User> Users { get; }
  DbSet<Workspace> Workspaces { get; }
  DbSet<WorkspaceMember> WorkspaceMembers { get; }
  DbSet<Board> Boards { get; }
  DbSet<Column> Columns { get; }
  DbSet<Card> Cards { get; }
  DbSet<CardAssignee> CardAssignees { get; }
  DbSet<CardLabel> CardLabels { get; }
  DbSet<Label> Labels { get; }
  DbSet<CardComment> CardComments { get; }
  DbSet<ActivityLog> ActivityLogs { get; }
  DbSet<Notification> Notifications { get; }
  DbSet<RefreshToken> RefreshTokens { get; }

  Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
  /// <summary>Id of the authenticated user. Throws when the request is anonymous.</summary>
  Guid UserId { get; }
}

/// <summary>Publishes realtime events to everyone viewing a board. Implemented with SignalR in the Api project.</summary>
public interface IBoardNotifier
{
  Task PublishAsync(BoardEvent boardEvent, CancellationToken cancellationToken = default);
}

public interface IIdentityService
{
  Task<Result<UserDto>> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken);

  Task<Result<UserDto>> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);

  Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface IAuthTokenService
{
  TimeSpan RefreshTokenLifetime { get; }

  AccessToken CreateAccessToken(UserDto user);

  string GenerateRefreshToken();

  string HashRefreshToken(string refreshToken);
}

/// <summary>Resolves which workspace owns a resource and what role a user has in it.</summary>
public interface IAccessChecker
{
  Task<Guid?> ResolveWorkspaceIdAsync(ResourceScope scope, CancellationToken cancellationToken);

  Task<WorkspaceRole?> GetRoleAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken);

  Task<bool> IsBoardMemberAsync(Guid boardId, Guid userId, CancellationToken cancellationToken);
}
