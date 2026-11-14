namespace TaskFlow.Domain.Common;

public abstract class BaseEntity
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public DateTime CreatedAt { get; set; }
  public DateTime? UpdatedAt { get; set; }
  public bool IsDeleted { get; set; }
  public DateTime? DeleteTime { get; set; }

  public void SoftDelete(DateTime utcNow)
  {
    IsDeleted = true;
    DeleteTime = utcNow;
  }
}
