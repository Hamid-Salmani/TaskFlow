namespace TaskFlow.Application.Common;

public enum ErrorType
{
  Validation,
  Unauthorized,
  Forbidden,
  NotFound,
  Conflict
}

public sealed record Error(
  ErrorType Type,
  string Code,
  string Message,
  IReadOnlyDictionary<string, string[]>? Errors = null)
{
  public static Error Validation(string code, string message) => new(ErrorType.Validation, code, message);

  public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
    new(ErrorType.Validation, "validation.failed", "One or more validation errors occurred.", errors);

  public static Error Unauthorized(string code, string message) => new(ErrorType.Unauthorized, code, message);

  public static Error Forbidden(string code, string message) => new(ErrorType.Forbidden, code, message);

  public static Error NotFound(string code, string message) => new(ErrorType.NotFound, code, message);

  public static Error Conflict(string code, string message) => new(ErrorType.Conflict, code, message);
}

/// <summary>Outcome of an operation that expects business failures (as opposed to exceptions).</summary>
public class Result
{
  public Result()
  {
  }

  public Result(Error error) => Error = error;

  public Error? Error { get; }
  public bool IsSuccess => Error is null;
  public bool IsFailure => Error is not null;

  public static Result Success() => new();

  public static Result Failure(Error error) => new(error);

  /// <summary>Creates a failed Result or Result&lt;T&gt; when only the response type is known (pipeline behaviours).</summary>
  public static TResponse FailureOf<TResponse>(Error error) where TResponse : Result =>
    (TResponse)Activator.CreateInstance(typeof(TResponse), error)!;

  public static implicit operator Result(Error error) => new(error);
}

public sealed class Result<T> : Result
{
  private readonly T? _value;

  public Result(T value) => _value = value;

  public Result(Error error) : base(error)
  {
  }

  public T Value => IsSuccess
    ? _value!
    : throw new InvalidOperationException("Cannot read the value of a failed result.");

  public static implicit operator Result<T>(T value) => new(value);

  public static implicit operator Result<T>(Error error) => new(error);
}
