using Microsoft.AspNetCore.Identity;

namespace TaskFlow.Domain.Entities;

public class User : IdentityUser<Guid>
{
  public string DisplayName { get; set; } = string.Empty;

  /// <summary>Unique, URL-safe handle used for @mentions.</summary>
  public string Handle { get; set; } = string.Empty;

  public DateTime CreatedAt { get; set; }
}
