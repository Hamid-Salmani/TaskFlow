using System.Security.Claims;
using TaskFlow.Application.Common;

namespace TaskFlow.Api.Services;

public static class ClaimsPrincipalExtensions
{
  public static Guid? GetUserId(this ClaimsPrincipal principal)
  {
    var value = principal.FindFirstValue("sub");
    return Guid.TryParse(value, out var id) ? id : null;
  }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
  public Guid UserId =>
    accessor.HttpContext?.User.GetUserId() ?? throw new UnauthorizedAccessException("No authenticated user.");
}
