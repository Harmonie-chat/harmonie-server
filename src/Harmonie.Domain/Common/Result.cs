namespace Harmonie.Domain.Common;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// Provides a functional approach to error handling without exceptions.
/// </summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("Success result cannot have an error");
        if (!isSuccess && error is null)
            throw new InvalidOperationException("Failure result must have an error");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);

    public Result Bind(Func<Result> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        return IsSuccess
            ? binder()
            : Failure(GetRequiredError());
    }

    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess
            ? onSuccess()
            : onFailure(GetRequiredError());
    }

    private string GetRequiredError() =>
        Error ?? throw new InvalidOperationException("Failure result must have an error");
}

/// <summary>
/// Represents the result of an operation that returns a value on success.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        if (isSuccess && value is null)
            throw new InvalidOperationException("Success result must have a value");
        if (isSuccess && error is not null)
            throw new InvalidOperationException("Success result cannot have an error");
        if (!isSuccess && error is null)
            throw new InvalidOperationException("Failure result must have an error");

        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);

    /// <summary>
    /// Maps the result value to a new type if successful
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return IsSuccess
            ? Result<TNew>.Success(mapper(GetRequiredValue()))
            : Result<TNew>.Failure(GetRequiredError());
    }

    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        return IsSuccess
            ? binder(GetRequiredValue())
            : Result<TNew>.Failure(GetRequiredError());
    }

    public Result Bind(Func<T, Result> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        return IsSuccess
            ? binder(GetRequiredValue())
            : Result.Failure(GetRequiredError());
    }

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess)
            action(GetRequiredValue());
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    public Result<T> OnFailure(Action<string> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsFailure)
            action(GetRequiredError());
        return this;
    }

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess
            ? onSuccess(GetRequiredValue())
            : onFailure(GetRequiredError());
    }

    private T GetRequiredValue()
    {
        if (Value is null)
            throw new InvalidOperationException("Success result must have a value");

        return Value;
    }

    private string GetRequiredError()
    {
        if (Error is null)
            throw new InvalidOperationException("Failure result must have an error");

        return Error;
    }
}
