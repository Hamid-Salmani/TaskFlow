using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class Card : BaseEntity, IPositioned
{
  public Guid BoardId { get; set; }
  public Guid ColumnId { get; set; }
  public string Title { get; set; } = string.Empty;
  public string? Description { get; set; }
  public double Position { get; set; }
  public DateTime? DueDate { get; set; }
  public Guid CreatedById { get; set; }

  /// <summary>Optimistic concurrency token. Incremented on every mutation of the card.</summary>
  public int Version { get; set; } = 1;

  public Board Board { get; set; } = null!;
  public Column Column { get; set; } = null!;
  public ICollection<CardAssignee> Assignees { get; set; } = new List<CardAssignee>();
  public ICollection<CardLabel> Labels { get; set; } = new List<CardLabel>();
  public ICollection<CardComment> Comments { get; set; } = new List<CardComment>();
}
