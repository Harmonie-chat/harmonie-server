using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Auth.LogoutAll;

/// <summary>
/// Handler for logging out all active sessions of the current authenticated user.
/// </summary>
public sealed class LogoutAllHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public LogoutAllHandler(IRefreshTokenRepository refreshTokenRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<ApplicationResponse<LogoutAllResponse>> HandleAsync(
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        await _refreshTokenRepository.RevokeAllActiveAsync(
            currentUserId,
            DateTime.UtcNow,
            RefreshTokenRevocationReasons.LogoutAll,
            cancellationToken);

        return ApplicationResponse<LogoutAllResponse>.Ok(new LogoutAllResponse());
    }
}
