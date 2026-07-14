namespace Store.Application.Common;

/// <summary>
/// A success flag, an optional error message, and (for the generic form) a value.
/// </summary>
public class Result
{
    public bool Success { get; protected init; }

    public string? Error { get; protected init; }

    public static Result Ok() => new() { Success = true };

    public static Result Fail(string error) => new() { Success = false, Error = error };

    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);

    public static Result<T> Fail<T>(string error) => Result<T>.Fail(error);
}

public sealed class Result<T> : Result
{
    public T? Value { get; private init; }

    public static Result<T> Ok(T value) => new() { Success = true, Value = value };

    public static new Result<T> Fail(string error) => new() { Success = false, Error = error };
}
