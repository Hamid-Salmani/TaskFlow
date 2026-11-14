using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class Workspace : BaseEntity
{
  public string Name { get; set; } = string.Empty;
  public string? Description { get; set; }

  public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
  public ICollection<Board> Boards { get; set; } = new List<Board>();
}
