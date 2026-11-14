using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Api.Middleware;

/// <summary>Last line of defence: turns unexpected exceptions into RFC 9457 problem details.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
{
  public async Task InvokeAsync(HttpContext context)
  {
    try
    {
      await next(context);
    }
    catch (DbUpdateConcurrencyException)
    {
      await WriteAsync(context, StatusCodes.Status409Conflict, "Conflict", "card.version_conflict",
        "The resource was modified by someone else. Reload and try again.");
    }
    catch (UnauthorizedAccessException)
    {
      await WriteAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized", "auth.unauthorized", "Authentication is required.");
    }
    catch (BadHttpRequestException exception)
    {
      await WriteAsync(context, StatusCodes.Status400BadRequest, "Bad Request", "request.malformed", exception.Message);
    }
    catch (Exception exception) when (context.RequestAborted.IsCancellationRequested == false)
    {
      logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
      var detail = environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.";
      await WriteAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error", "server.error", detail);
    }
  }

  private static async Task WriteAsync(HttpContext context, int status, string title, string code, string detail)
  {
    if (context.Response.HasStarted)
    {
      return;
    }

    var problem = new ProblemDetails
    {
      Type = $"https://httpstatuses.com/{status}",
      Title = title,
      Status = status,
      Detail = detail
    };
    problem.Extensions["code"] = code;
    problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

    context.Response.Clear();
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions)null, "application/problem+json");
  }
}
