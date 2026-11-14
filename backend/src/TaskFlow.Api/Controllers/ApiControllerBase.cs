using System.Diagnostics;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common;

namespace TaskFlow.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
  protected ActionResult<T> Respond<T>(Result<T> result) =>
    result.IsSuccess ? result.Value : Problem(result.Error!);

  protected ActionResult Respond(Result result) =>
    result.IsSuccess ? NoContent() : Problem(result.Error!);

  protected ActionResult<T> RespondCreated<T>(Result<T> result) =>
    result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : Problem(result.Error!);

  private ObjectResult Problem(Error error)
  {
    var status = error.Type switch
    {
      ErrorType.Validation => StatusCodes.Status400BadRequest,
      ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
      ErrorType.Forbidden => StatusCodes.Status403Forbidden,
      ErrorType.NotFound => StatusCodes.Status404NotFound,
      ErrorType.Conflict => StatusCodes.Status409Conflict,
      _ => StatusCodes.Status500InternalServerError
    };

    ProblemDetails problem = error.Errors is null
      ? new ProblemDetails()
      : new ValidationProblemDetails(error.Errors.ToDictionary(a => a.Key, a => a.Value));

    problem.Type = $"https://httpstatuses.com/{status}";
    problem.Title = error.Type switch
    {
      ErrorType.Validation => "Bad Request",
      ErrorType.Unauthorized => "Unauthorized",
      ErrorType.Forbidden => "Forbidden",
      ErrorType.NotFound => "Not Found",
      ErrorType.Conflict => "Conflict",
      _ => "Error"
    };
    problem.Status = status;
    problem.Detail = error.Message;
    problem.Extensions["code"] = error.Code;
    problem.Extensions["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

    return new ObjectResult(problem)
    {
      StatusCode = status,
      ContentTypes = { "application/problem+json" }
    };
  }
}

public static class ApiRoutes
{
  public const string Base = "api/v{version:apiVersion}";
}
