using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;

namespace Harmonie.Application.Features.Auth.RefreshToken;

/// <summary>
/// Handler for refresh token flow with token rotation.
/// </summary>
public sealed class RefreshTokenHandler
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
        var refreshTokenHash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        var session = await _refreshTokenRepository.GetByTokenHashAsync(refreshTokenHash, cancellationToken);
        var nowUtc = DateTime.UtcNow;

        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= nowUtc)
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
            return ApplicationResponse<RefreshTokenResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidRefreshToken,
                "Refresh token is invalid or expired");

        var payload = new RefreshTokenResponse(
            AccessToken: accessToken,
            RefreshToken: newRefreshToken,
            ExpiresAt: accessTokenExpiresAt);

        return ApplicationResponse<RefreshTokenResponse>.Ok(payload);
    }
}
