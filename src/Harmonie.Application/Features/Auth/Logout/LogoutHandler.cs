using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Harmonie.Application.Interfaces.Auth;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Auth.Logout;

/// <summary>
/// Handler for logging out the current authenticated session.
/// </summary>
public sealed class LogoutHandler : IAuthenticatedHandler<LogoutRequest, LogoutResponse>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _jwtTokenService;

    public LogoutHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenService jwtTokenService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ApplicationResponse<LogoutResponse>> HandleAsync(
        LogoutRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var refreshTokenHash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        var revoked = await _refreshTokenRepository.RevokeActiveAsync(
            currentUserId,
            refreshTokenHash,
            DateTime.UtcNow,
            RefreshTokenRevocationReasons.Logout,
            cancellationToken);

        if (!revoked)
        {
            return ApplicationResponse<LogoutResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidRefreshToken,
                "Refresh token is invalid or expired");
        }

        return ApplicationResponse<LogoutResponse>.Ok(new LogoutResponse());
    }
}
