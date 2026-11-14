using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class Board : BaseEntity
{
  public Guid WorkspaceId { get; set; }
  public string Name { get; set; } = string.Empty;
  public string? Description { get; set; }
  public bool IsArchived { get; set; }

  public Workspace Workspace { get; set; } = null!;
  public ICollection<Column> Columns { get; set; } = new List<Column>();
  public ICollection<Label> Labels { get; set; } = new List<Label>();
}
