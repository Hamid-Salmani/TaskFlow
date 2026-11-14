using FluentValidation;
using MediatR;

namespace TaskFlow.Application.Common;

public sealed class AuthorizationBehavior<TRequest, TResponse>(
  IAccessChecker accessChecker,
  ICurrentUser currentUser,
  AuthorizationContext authorizationContext) : IPipelineBehavior<TRequest, TResponse>
  where TRequest : notnull
  where TResponse : Result
{
  public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
  {
    if (request is not IAuthorizedRequest authorized)
    {
      return await next();
    }

    var workspaceId = await accessChecker.ResolveWorkspaceIdAsync(authorized.Scope, cancellationToken);
    if (workspaceId is null)
    {
      return Result.FailureOf<TResponse>(Error.NotFound("resource.not_found", "The requested resource was not found."));
    }

    var role = await accessChecker.GetRoleAsync(workspaceId.Value, currentUser.UserId, cancellationToken);
    if (role is null)
    {
      // Non-members get the same answer as for a missing resource so existence is not leaked.
      return Result.FailureOf<TResponse>(Error.NotFound("resource.not_found", "The requested resource was not found."));
    }

    if (role < authorized.MinimumRole)
    {
      return Result.FailureOf<TResponse>(Error.Forbidden("resource.forbidden", "You do not have permission to perform this action."));
    }

    authorizationContext.WorkspaceId = workspaceId.Value;
    authorizationContext.Role = role.Value;
    return await next();
  }
}

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
  : IPipelineBehavior<TRequest, TResponse>
  where TRequest : notnull
  where TResponse : Result
{
  public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
  {
    if (validators.Any() == false)
    {
      return await next();
    }

    var context = new ValidationContext<TRequest>(request);
    var results = await Task.WhenAll(validators.Select(a => a.ValidateAsync(context, cancellationToken)));
    var failures = results.SelectMany(a => a.Errors).Where(a => a is not null).ToList();
    if (failures.Count == 0)
    {
      return await next();
    }

    var errors = failures
      .GroupBy(a => ToCamelCase(a.PropertyName))
      .ToDictionary(a => a.Key, a => a.Select(b => b.ErrorMessage).Distinct().ToArray());

    return Result.FailureOf<TResponse>(Error.Validation(errors));
  }

  private static string ToCamelCase(string name) =>
    string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
