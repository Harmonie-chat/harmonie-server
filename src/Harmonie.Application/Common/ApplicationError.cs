namespace Harmonie.Application.Common;

/// <summary>
/// Standardized application error contract.
/// </summary>
public sealed record ApplicationError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null);
