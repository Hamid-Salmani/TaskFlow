using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Identity;

public sealed partial class IdentityService(UserManager<User> userManager, TimeProvider timeProvider) : IIdentityService
{
  public async Task<Result<UserDto>> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken)
  {
    var normalizedEmail = email.Trim().ToLowerInvariant();
    if (await userManager.FindByEmailAsync(normalizedEmail) is not null)
    {
      return Error.Conflict("auth.email_taken", "An account with this email already exists.");
    }

    var user = new User
    {
      Id = Guid.NewGuid(),
      UserName = normalizedEmail,
      Email = normalizedEmail,
      DisplayName = displayName.Trim(),
      Handle = await GenerateHandleAsync(normalizedEmail, cancellationToken),
      CreatedAt = timeProvider.GetUtcNow().UtcDateTime
    };

    var result = await userManager.CreateAsync(user, password);
    if (result.Succeeded == false)
    {
      var errors = result.Errors
        .GroupBy(a => a.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email")
        .ToDictionary(a => a.Key, a => a.Select(b => b.Description).ToArray());
      return Error.Validation(errors);
    }

    return ToDto(user);
  }

  public async Task<Result<UserDto>> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken)
  {
    var user = await userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
    if (user is null || await userManager.CheckPasswordAsync(user, password) == false)
    {
      return Error.Unauthorized("auth.invalid_credentials", "Invalid email or password.");
    }

    return ToDto(user);
  }

  public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
  {
    var user = await userManager.Users.AsNoTracking().FirstOrDefaultAsync(a => a.Id == userId, cancellationToken);
    return user is null ? null : ToDto(user);
  }

  private async Task<string> GenerateHandleAsync(string email, CancellationToken cancellationToken)
  {
    var localPart = email.Split('@')[0];
    var baseHandle = InvalidHandleCharacters().Replace(localPart, string.Empty);
    baseHandle = baseHandle.Length == 0 ? "user" : baseHandle[..Math.Min(baseHandle.Length, 30)];

    var candidate = baseHandle;
    while (await userManager.Users.AnyAsync(a => a.Handle == candidate, cancellationToken))
    {
      candidate = $"{baseHandle}{Random.Shared.Next(100, 10000)}";
    }

    return candidate;
  }

  private static UserDto ToDto(User user) => new(user.Id, user.Email!, user.DisplayName, user.Handle);

  [GeneratedRegex("[^a-z0-9_.-]")]
  private static partial Regex InvalidHandleCharacters();
}
