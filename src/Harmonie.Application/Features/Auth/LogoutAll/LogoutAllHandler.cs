using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Auth.LogoutAll;

/// <summary>
/// Handler for logging out all active sessions of the current authenticated user.
/// </summary>
public sealed class LogoutAllHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<LogoutAllHandler> _logger;

    public LogoutAllHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<LogoutAllHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<LogoutAllResponse>> HandleAsync(
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("LogoutAll started. UserId={UserId}", currentUserId);

        await _refreshTokenRepository.RevokeAllActiveAsync(
            currentUserId,
            DateTime.UtcNow,
            RefreshTokenRevocationReasons.LogoutAll,
            cancellationToken);

        _logger.LogInformation("LogoutAll succeeded. UserId={UserId}", currentUserId);
        return ApplicationResponse<LogoutAllResponse>.Ok(new LogoutAllResponse());
    }
}
