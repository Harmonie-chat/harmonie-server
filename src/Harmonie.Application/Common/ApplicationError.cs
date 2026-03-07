namespace Harmonie.Application.Common;

/// <summary>
/// Standardized application error contract.
/// </summary>
public sealed record ApplicationError(
    string Code,
    string Detail,
    IReadOnlyDictionary<string, ApplicationValidationError[]>? Errors = null,
    int? Status = null,
    string? TraceId = null);

public sealed record ApplicationValidationError(
    string Code,
    string Detail);
