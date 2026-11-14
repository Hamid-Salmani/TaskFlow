using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class Column : BaseEntity, IPositioned
{
  public Guid BoardId { get; set; }
  public string Title { get; set; } = string.Empty;
  public double Position { get; set; }

  public Board Board { get; set; } = null!;
  public ICollection<Card> Cards { get; set; } = new List<Card>();
}
