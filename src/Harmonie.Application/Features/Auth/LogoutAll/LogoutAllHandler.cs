using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Harmonie.Application.Interfaces.Auth;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Auth.LogoutAll;

/// <summary>
/// Handler for logging out all active sessions of the current authenticated user.
/// </summary>
public sealed class LogoutAllHandler : IAuthenticatedHandler<Unit, LogoutAllResponse>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly TimeProvider _timeProvider;

    public LogoutAllHandler(
        IRefreshTokenRepository refreshTokenRepository,
        TimeProvider timeProvider)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _timeProvider = timeProvider;
    }

    public async Task<ApplicationResponse<LogoutAllResponse>> HandleAsync(
        Unit request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        await _refreshTokenRepository.RevokeAllActiveAsync(
            currentUserId,
            _timeProvider.GetUtcNow().UtcDateTime,
            RefreshTokenRevocationReasons.LogoutAll,
            cancellationToken);

        return ApplicationResponse<LogoutAllResponse>.Ok(new LogoutAllResponse());
    }
}
