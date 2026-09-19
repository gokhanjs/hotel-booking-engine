using System.Diagnostics.CodeAnalysis;

namespace BookingEngine.Application.Common;

public enum ErrorType
{
    NotFound,
    Conflict,
    Unprocessable,
}

public sealed record Error(ErrorType Type, string Code, string Message)
{
    public static Error NotFound(string resource) =>
        new(ErrorType.NotFound, $"{resource}.not_found", $"{char.ToUpperInvariant(resource[0])}{resource[1..].Replace('_', ' ')} was not found.");
    public static Error Conflict(string code, string message) => new(ErrorType.Conflict, code, message);
    public static Error Unprocessable(string code, string message) => new(ErrorType.Unprocessable, code, message);
}

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static implicit operator Result(Error error) => new(error);
}

public sealed class Result<T> : Result
{
    private Result(T? value, Error? error) : base(error) => Value = value;

    public T? Value { get; }

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(Error error) => new(default, error);
}
