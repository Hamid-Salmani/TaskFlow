using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Infrastructure.Identity;

public sealed class JwtOptions
{
  public const string Section = "Jwt";

  public string Issuer { get; set; } = "TaskFlow";
  public string Audience { get; set; } = "TaskFlow.Client";

  /// <summary>HMAC-SHA256 signing key, at least 32 characters. Override outside development.</summary>
  public string Key { get; set; } = string.Empty;

  public int AccessTokenMinutes { get; set; } = 15;
  public int RefreshTokenDays { get; set; } = 14;
}

public sealed class AuthTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAuthTokenService
{
  private readonly JwtOptions _options = options.Value;

  public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

  public AccessToken CreateAccessToken(UserDto user)
  {
    var now = timeProvider.GetUtcNow().UtcDateTime;
    var expires = now.AddMinutes(_options.AccessTokenMinutes);
    var credentials = new SigningCredentials(
      new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
      SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
      new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
      new Claim(JwtRegisteredClaimNames.Email, user.Email),
      new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
      new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now, expires, credentials);
    return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
  }

  public string GenerateRefreshToken() =>
    Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
      .Replace('+', '-')
      .Replace('/', '_')
      .TrimEnd('=');

  public string HashRefreshToken(string refreshToken) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();
}
