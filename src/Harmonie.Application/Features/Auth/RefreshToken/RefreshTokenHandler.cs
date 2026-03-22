using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Harmonie.Application.Interfaces.Auth;
using Harmonie.Application.Interfaces.Users;

namespace Harmonie.Application.Features.Auth.RefreshToken;

/// <summary>
/// Handler for refresh token flow with token rotation.
/// </summary>
public sealed class RefreshTokenHandler : IHandler<RefreshTokenRequest, RefreshTokenResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _jwtTokenService;

    public RefreshTokenHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ApplicationResponse<RefreshTokenResponse>> HandleAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var refreshTokenHash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        var session = await _refreshTokenRepository.GetByTokenHashAsync(refreshTokenHash, cancellationToken);

        if (session is null)
            return ApplicationResponse<RefreshTokenResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidRefreshToken,
                "Refresh token is invalid or expired");

        if (session.RevokedAtUtc is not null)
            return await HandleReuseDetectedAsync(session, nowUtc, cancellationToken);

        if (session.ExpiresAtUtc <= nowUtc)
            return ApplicationResponse<RefreshTokenResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidRefreshToken,
                "Refresh token is invalid or expired");

        var user = await _userRepository.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null)
            return ApplicationResponse<RefreshTokenResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidRefreshToken,
                "Refresh token is invalid or expired");

        if (!user.IsActive)
            return ApplicationResponse<RefreshTokenResponse>.Fail(
                ApplicationErrorCodes.Auth.UserInactive,
                $"User with ID '{user.Id}' is inactive and cannot perform this operation");

        var accessToken = _jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Username);
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _jwtTokenService.HashRefreshToken(newRefreshToken);

        var accessTokenExpiresAt = _jwtTokenService.GetAccessTokenExpirationUtc();
        var refreshTokenExpiresAt = _jwtTokenService.GetRefreshTokenExpirationUtc();

        var rotated = await _refreshTokenRepository.RotateAsync(
            session.Id,
            user.Id,
            newRefreshTokenHash,
            refreshTokenExpiresAt,
            nowUtc,
            cancellationToken);

        if (!rotated)
        {
            var latestSession = await _refreshTokenRepository.GetByTokenHashAsync(refreshTokenHash, cancellationToken);
            if (latestSession?.RevokedAtUtc is not null)
                return await HandleReuseDetectedAsync(latestSession, nowUtc, cancellationToken);

            return ApplicationResponse<RefreshTokenResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidRefreshToken,
                "Refresh token is invalid or expired");
        }

        var payload = new RefreshTokenResponse(
            AccessToken: accessToken,
            RefreshToken: newRefreshToken,
            ExpiresAt: accessTokenExpiresAt);

        return ApplicationResponse<RefreshTokenResponse>.Ok(payload);
    }

    private async Task<ApplicationResponse<RefreshTokenResponse>> HandleReuseDetectedAsync(
        RefreshTokenSession reusedSession,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken)
    {
        await _refreshTokenRepository.RevokeActiveFamilyAsync(
            reusedSession.Id,
            revokedAtUtc,
            RefreshTokenRevocationReasons.ReuseDetected,
            cancellationToken);

        // Upgrade safety: historical rotated tokens may miss replacement linkage.
        // In that specific case, revoke all active sessions for the user to avoid leaving compromised access active.
        var shouldFallbackToRevokeAll = reusedSession.ReplacedByTokenId is null
            && (string.IsNullOrWhiteSpace(reusedSession.RevocationReason)
                || reusedSession.RevocationReason == RefreshTokenRevocationReasons.Rotated);

        if (shouldFallbackToRevokeAll)
        {
            await _refreshTokenRepository.RevokeAllActiveAsync(
                reusedSession.UserId,
                revokedAtUtc,
                RefreshTokenRevocationReasons.ReuseDetected,
                cancellationToken);
        }

        return ApplicationResponse<RefreshTokenResponse>.Fail(
            ApplicationErrorCodes.Auth.RefreshTokenReuseDetected,
            "Refresh token reuse detected; associated sessions were revoked");
    }
}
