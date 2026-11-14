using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class Notification
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid UserId { get; set; }
  public Guid? ActorId { get; set; }
  public NotificationType Type { get; set; }
  public string Message { get; set; } = string.Empty;
  public Guid BoardId { get; set; }
  public Guid? CardId { get; set; }
  public bool IsRead { get; set; }
  public DateTime CreatedAt { get; set; }

  public User User { get; set; } = null!;
}
