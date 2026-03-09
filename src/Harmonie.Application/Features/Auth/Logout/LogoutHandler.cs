using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Auth.Logout;

/// <summary>
/// Handler for logging out the current authenticated session.
/// </summary>
public sealed class LogoutHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenService jwtTokenService,
        ILogger<LogoutHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<ApplicationResponse<LogoutResponse>> HandleAsync(
        LogoutRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Logout started. UserId={UserId}", currentUserId);

        var refreshTokenHash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        var revoked = await _refreshTokenRepository.RevokeActiveAsync(
            currentUserId,
            refreshTokenHash,
            DateTime.UtcNow,
            RefreshTokenRevocationReasons.Logout,
            cancellationToken);

        if (!revoked)
        {
            _logger.LogWarning("Logout failed because refresh token was invalid. UserId={UserId}", currentUserId);

            return ApplicationResponse<LogoutResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidRefreshToken,
                "Refresh token is invalid or expired");
        }

        _logger.LogInformation("Logout succeeded. UserId={UserId}", currentUserId);
        return ApplicationResponse<LogoutResponse>.Ok(new LogoutResponse());
    }
}
