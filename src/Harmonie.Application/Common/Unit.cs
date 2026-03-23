namespace Harmonie.Application.Common;

/// <summary>
/// Empty request type for handlers that require no input beyond identity.
/// </summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
