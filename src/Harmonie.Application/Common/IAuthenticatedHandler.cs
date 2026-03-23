using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common;

/// <summary>
/// Interface for authenticated request handlers that receive the current user's identity.
/// </summary>
public interface IAuthenticatedHandler<TRequest, TResponse>
{
    Task<ApplicationResponse<TResponse>> HandleAsync(
        TRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default);
}
