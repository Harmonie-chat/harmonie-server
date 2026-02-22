namespace Harmonie.Application.Common;

/// <summary>
/// Standardized application response envelope for both success and error flows.
/// </summary>
public sealed record ApplicationResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApplicationError? Error { get; init; }

    public static ApplicationResponse<T> Ok(T data)
        => new()
        {
            Success = true,
            Data = data,
            Error = null
        };

    public static ApplicationResponse<T> Fail(ApplicationError error)
        => new()
        {
            Success = false,
            Data = default,
            Error = error
        };

    public static ApplicationResponse<T> Fail(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]>? details = null)
        => new()
        {
            Success = false,
            Data = default,
            Error = new ApplicationError(code, message, details)
        };
}
