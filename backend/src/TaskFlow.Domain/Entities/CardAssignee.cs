namespace TaskFlow.Domain.Entities;

/// <summary>Explicit join entity between Card and User.</summary>
public class CardAssignee
{
  public Guid CardId { get; set; }
  public Guid UserId { get; set; }
  public DateTime AssignedAt { get; set; }

  public Card Card { get; set; } = null!;
  public User User { get; set; } = null!;
}
