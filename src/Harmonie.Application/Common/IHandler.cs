namespace Harmonie.Application.Common;

/// <summary>
/// Interface for unauthenticated request handlers.
/// </summary>
public interface IHandler<TRequest, TResponse>
{
    Task<ApplicationResponse<TResponse>> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}
