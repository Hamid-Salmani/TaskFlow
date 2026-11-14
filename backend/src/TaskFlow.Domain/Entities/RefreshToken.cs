namespace TaskFlow.Domain.Entities;

/// <summary>
/// Only the SHA-256 hash of the token is stored. Tokens that belong to the same login share a
/// <see cref="FamilyId"/> so a reused (already rotated) token can revoke the whole family.
/// </summary>
public class RefreshToken
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid UserId { get; set; }
  public Guid FamilyId { get; set; }
  public string TokenHash { get; set; } = string.Empty;
  public DateTime CreatedAt { get; set; }
  public DateTime ExpiresAt { get; set; }
  public DateTime? RevokedAt { get; set; }
  public string? ReplacedByTokenHash { get; set; }

  public User User { get; set; } = null!;

  public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
