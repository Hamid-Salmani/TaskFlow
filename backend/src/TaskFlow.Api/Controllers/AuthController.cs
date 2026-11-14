using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Contracts;
using TaskFlow.Api.Extensions;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Features.Auth;

namespace TaskFlow.Api.Controllers;

[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController(ISender sender) : ApiControllerBase
{
  [AllowAnonymous]
  [EnableRateLimiting(ApiPolicies.AuthRateLimit)]
  [HttpPost("register")]
  [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
  public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request) =>
    RespondCreated(await sender.Send(new RegisterCommand(request.Email, request.DisplayName, request.Password)));

  [AllowAnonymous]
  [EnableRateLimiting(ApiPolicies.AuthRateLimit)]
  [HttpPost("login")]
  public async Task<ActionResult<AuthResponse>> Login(LoginRequest request) =>
    Respond(await sender.Send(new LoginCommand(request.Email, request.Password)));

  [AllowAnonymous]
  [EnableRateLimiting(ApiPolicies.AuthRateLimit)]
  [HttpPost("refresh")]
  public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request) =>
    Respond(await sender.Send(new RefreshCommand(request.RefreshToken)));

  [AllowAnonymous]
  [HttpPost("logout")]
  public async Task<ActionResult> Logout(RefreshRequest request) =>
    Respond(await sender.Send(new LogoutCommand(request.RefreshToken)));

  [HttpGet("me")]
  public async Task<ActionResult<UserDto>> Me() =>
    Respond(await sender.Send(new GetMeQuery()));
}
