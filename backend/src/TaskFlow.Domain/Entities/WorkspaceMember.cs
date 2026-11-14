using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

/// <summary>Explicit join entity between Workspace and User (composite key).</summary>
public class WorkspaceMember
{
  public Guid WorkspaceId { get; set; }
  public Guid UserId { get; set; }
  public WorkspaceRole Role { get; set; }
  public DateTime JoinedAt { get; set; }

  public Workspace Workspace { get; set; } = null!;
  public User User { get; set; } = null!;
}
