using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Features.Auth;

/// <summary>Creates an access token and a (rotated) refresh token for a user.</summary>
public sealed class SessionIssuer(IApplicationDbContext db, IAuthTokenService tokens, TimeProvider timeProvider)
{
  public async Task<AuthResponse> IssueAsync(UserDto user, Guid? familyId, RefreshToken? rotatedFrom, CancellationToken cancellationToken)
  {
    var now = timeProvider.GetUtcNow().UtcDateTime;
    var access = tokens.CreateAccessToken(user);
    var refreshToken = tokens.GenerateRefreshToken();
    var hash = tokens.HashRefreshToken(refreshToken);

    db.RefreshTokens.Add(new RefreshToken
    {
      UserId = user.Id,
      FamilyId = familyId ?? Guid.NewGuid(),
      TokenHash = hash,
      CreatedAt = now,
      ExpiresAt = now.Add(tokens.RefreshTokenLifetime)
    });

    if (rotatedFrom is not null)
    {
      rotatedFrom.RevokedAt = now;
      rotatedFrom.ReplacedByTokenHash = hash;
    }

    await db.SaveChangesAsync(cancellationToken);
    return new AuthResponse(access.Token, access.ExpiresAt, refreshToken, user);
  }
}

// ---------- Register ----------

public sealed record RegisterCommand(string Email, string DisplayName, string Password) : IRequest<Result<AuthResponse>>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
  public RegisterCommandValidator()
  {
    RuleFor(a => a.Email).NotEmpty().EmailAddress().MaximumLength(256);
    RuleFor(a => a.DisplayName).NotEmpty().MaximumLength(100);
    RuleFor(a => a.Password)
      .NotEmpty()
      .MinimumLength(8)
      .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
      .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.");
  }
}

public sealed class RegisterCommandHandler(IIdentityService identity, SessionIssuer sessions)
  : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
  public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
  {
    var user = await identity.RegisterAsync(request.Email, request.DisplayName, request.Password, cancellationToken);
    if (user.IsFailure)
    {
      return user.Error!;
    }

    return await sessions.IssueAsync(user.Value, null, null, cancellationToken);
  }
}

// ---------- Login ----------

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
  public LoginCommandValidator()
  {
    RuleFor(a => a.Email).NotEmpty();
    RuleFor(a => a.Password).NotEmpty();
  }
}

public sealed class LoginCommandHandler(IIdentityService identity, SessionIssuer sessions)
  : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
  public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
  {
    var user = await identity.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);
    if (user.IsFailure)
    {
      return user.Error!;
    }

    return await sessions.IssueAsync(user.Value, null, null, cancellationToken);
  }
}

// ---------- Refresh (rotation + reuse detection) ----------

public sealed record RefreshCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
  public RefreshCommandValidator() => RuleFor(a => a.RefreshToken).NotEmpty();
}

public sealed class RefreshCommandHandler(
  IApplicationDbContext db,
  IAuthTokenService tokens,
  IIdentityService identity,
  SessionIssuer sessions,
  TimeProvider timeProvider) : IRequestHandler<RefreshCommand, Result<AuthResponse>>
{
  public async Task<Result<AuthResponse>> Handle(RefreshCommand request, CancellationToken cancellationToken)
  {
    var now = timeProvider.GetUtcNow().UtcDateTime;
    var hash = tokens.HashRefreshToken(request.RefreshToken);
    var stored = await db.RefreshTokens.FirstOrDefaultAsync(a => a.TokenHash == hash, cancellationToken);
    if (stored is null)
    {
      return Error.Unauthorized("auth.invalid_refresh_token", "The refresh token is not valid.");
    }

    if (stored.RevokedAt is not null)
    {
      // A rotated token was presented again: assume it leaked and kill the whole login session.
      var family = await db.RefreshTokens
        .Where(a => a.FamilyId == stored.FamilyId && a.RevokedAt == null)
        .ToListAsync(cancellationToken);
      foreach (var token in family)
      {
        token.RevokedAt = now;
      }

      await db.SaveChangesAsync(cancellationToken);
      return Error.Unauthorized("auth.refresh_token_reused", "The refresh token was already used.");
    }

    if (stored.IsActive(now) == false)
    {
      return Error.Unauthorized("auth.refresh_token_expired", "The refresh token has expired.");
    }

    var user = await identity.GetUserAsync(stored.UserId, cancellationToken);
    if (user is null)
    {
      return Error.Unauthorized("auth.invalid_refresh_token", "The refresh token is not valid.");
    }

    return await sessions.IssueAsync(user, stored.FamilyId, stored, cancellationToken);
  }
}

// ---------- Logout ----------

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;

public sealed class LogoutCommandHandler(IApplicationDbContext db, IAuthTokenService tokens, TimeProvider timeProvider)
  : IRequestHandler<LogoutCommand, Result>
{
  public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
  {
    var hash = tokens.HashRefreshToken(request.RefreshToken);
    var stored = await db.RefreshTokens.FirstOrDefaultAsync(a => a.TokenHash == hash, cancellationToken);
    if (stored is null)
    {
      return Result.Success();
    }

    var now = timeProvider.GetUtcNow().UtcDateTime;
    var family = await db.RefreshTokens
      .Where(a => a.FamilyId == stored.FamilyId && a.RevokedAt == null)
      .ToListAsync(cancellationToken);
    foreach (var token in family)
    {
      token.RevokedAt = now;
    }

    await db.SaveChangesAsync(cancellationToken);
    return Result.Success();
  }
}

// ---------- Me ----------

public sealed record GetMeQuery : IRequest<Result<UserDto>>;

public sealed class GetMeQueryHandler(IIdentityService identity, ICurrentUser currentUser)
  : IRequestHandler<GetMeQuery, Result<UserDto>>
{
  public async Task<Result<UserDto>> Handle(GetMeQuery request, CancellationToken cancellationToken)
  {
    var user = await identity.GetUserAsync(currentUser.UserId, cancellationToken);
    if (user is null)
    {
      return Error.NotFound("user.not_found", "The user no longer exists.");
    }

    return user;
  }
}
