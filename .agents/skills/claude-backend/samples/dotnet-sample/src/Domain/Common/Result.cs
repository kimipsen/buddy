using System;

namespace Project.Domain.Common;

// Prefer returning Result / Result<T> over throwing for expected, recoverable failures — domain
// rule violations, validation errors, not-found. Callers then handle failure as a value (pattern
// match / if-check) instead of via try/catch. Reserve exceptions for conditions the caller cannot
// reasonably handle in-line: programmer errors, corrupt/unmapped data, infrastructure failures.
public readonly struct Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    private Result(bool isSuccess, string? error) => (IsSuccess, Error) = (isSuccess, error);

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
}

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error) => (IsSuccess, Value, Error) = (isSuccess, value, error);

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);

    public static implicit operator Result<T>(T value) => Success(value);
}
