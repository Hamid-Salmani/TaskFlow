using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class ActivityLog
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid BoardId { get; set; }
  public Guid? CardId { get; set; }
  public Guid UserId { get; set; }
  public ActivityType Type { get; set; }
  public string Message { get; set; } = string.Empty;
  public DateTime CreatedAt { get; set; }

  public User User { get; set; } = null!;
}
