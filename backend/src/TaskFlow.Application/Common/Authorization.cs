using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common;

public enum ScopeKind
{
  Workspace,
  Board,
  Column,
  Card,
  Label,
  Comment
}

public readonly record struct ResourceScope(ScopeKind Kind, Guid Id)
{
  public static ResourceScope Workspace(Guid id) => new(ScopeKind.Workspace, id);

  public static ResourceScope Board(Guid id) => new(ScopeKind.Board, id);

  public static ResourceScope Column(Guid id) => new(ScopeKind.Column, id);

  public static ResourceScope Card(Guid id) => new(ScopeKind.Card, id);

  public static ResourceScope Label(Guid id) => new(ScopeKind.Label, id);

  public static ResourceScope Comment(Guid id) => new(ScopeKind.Comment, id);
}

/// <summary>
/// Implemented by requests that touch a workspace resource. The <c>AuthorizationBehavior</c> checks
/// membership and the minimum role before the handler runs.
/// </summary>
public interface IAuthorizedRequest
{
  ResourceScope Scope { get; }

  WorkspaceRole MinimumRole { get; }
}

/// <summary>Filled by the authorization behaviour, readable by handlers that need the caller's role.</summary>
public interface IAuthorizationContext
{
  Guid WorkspaceId { get; }

  WorkspaceRole Role { get; }
}

public sealed class AuthorizationContext : IAuthorizationContext
{
  public Guid WorkspaceId { get; set; }

  public WorkspaceRole Role { get; set; }
}

public sealed class AccessChecker(IApplicationDbContext db) : IAccessChecker
{
  public async Task<Guid?> ResolveWorkspaceIdAsync(ResourceScope scope, CancellationToken cancellationToken)
  {
    var id = scope.Id;

    // Soft-deleted parents are checked explicitly so a deleted workspace/board hides everything below it.
    return scope.Kind switch
    {
      ScopeKind.Workspace => await db.Workspaces
        .Where(a => a.Id == id)
        .Select(a => (Guid?)a.Id)
        .FirstOrDefaultAsync(cancellationToken),
      ScopeKind.Board => await db.Boards
        .Where(a => a.Id == id && a.Workspace.IsDeleted == false)
        .Select(a => (Guid?)a.WorkspaceId)
        .FirstOrDefaultAsync(cancellationToken),
      ScopeKind.Column => await db.Columns
        .Where(a => a.Id == id && a.Board.IsDeleted == false && a.Board.Workspace.IsDeleted == false)
        .Select(a => (Guid?)a.Board.WorkspaceId)
        .FirstOrDefaultAsync(cancellationToken),
      ScopeKind.Card => await db.Cards
        .Where(a => a.Id == id && a.Board.IsDeleted == false && a.Board.Workspace.IsDeleted == false)
        .Select(a => (Guid?)a.Board.WorkspaceId)
        .FirstOrDefaultAsync(cancellationToken),
      ScopeKind.Label => await db.Labels
        .Where(a => a.Id == id && a.Board.IsDeleted == false && a.Board.Workspace.IsDeleted == false)
        .Select(a => (Guid?)a.Board.WorkspaceId)
        .FirstOrDefaultAsync(cancellationToken),
      ScopeKind.Comment => await db.CardComments
        .Where(a => a.Id == id && a.Card.IsDeleted == false && a.Card.Board.IsDeleted == false && a.Card.Board.Workspace.IsDeleted == false)
        .Select(a => (Guid?)a.Card.Board.WorkspaceId)
        .FirstOrDefaultAsync(cancellationToken),
      _ => null
    };
  }

  public Task<WorkspaceRole?> GetRoleAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken) =>
    db.WorkspaceMembers
      .AsNoTracking()
      .Where(a => a.WorkspaceId == workspaceId && a.UserId == userId)
      .Select(a => (WorkspaceRole?)a.Role)
      .FirstOrDefaultAsync(cancellationToken);

  public async Task<bool> IsBoardMemberAsync(Guid boardId, Guid userId, CancellationToken cancellationToken)
  {
    var workspaceId = await ResolveWorkspaceIdAsync(ResourceScope.Board(boardId), cancellationToken);
    if (workspaceId is null)
    {
      return false;
    }

    return await GetRoleAsync(workspaceId.Value, userId, cancellationToken) is not null;
  }
}
