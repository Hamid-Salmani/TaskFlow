using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public class CardComment : BaseEntity
{
  public Guid CardId { get; set; }
  public Guid AuthorId { get; set; }
  public string Body { get; set; } = string.Empty;
  public DateTime? EditedAt { get; set; }

  public Card Card { get; set; } = null!;
  public User Author { get; set; } = null!;
}
