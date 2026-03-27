using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Auth;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Auth.Login;

/// <summary>
/// Handler for user login business logic
/// </summary>
public sealed class LoginHandler : IHandler<LoginRequest, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<ApplicationResponse<LoginResponse>> HandleAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // Try to find user by email or username
        var emailResult = Email.Create(request.EmailOrUsername);
        var usernameResult = Username.Create(request.EmailOrUsername);

        User? user = null;
        if (emailResult.IsSuccess && emailResult.Value is not null)
            user = await _userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);
        else if (usernameResult.IsSuccess && usernameResult.Value is not null)
            user = await _userRepository.GetByUsernameAsync(usernameResult.Value, cancellationToken);

        if (user is null)
            return ApplicationResponse<LoginResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidCredentials,
                "Invalid email/username or password");

        if (!user.IsActive)
            return ApplicationResponse<LoginResponse>.Fail(
                ApplicationErrorCodes.Auth.UserInactive,
                $"User with ID '{user.Id}' is inactive and cannot perform this operation");

        // Verify password
        if (!_passwordHasher.VerifyPassword(user.Email, user.PasswordHash, request.Password))
            return ApplicationResponse<LoginResponse>.Fail(
                ApplicationErrorCodes.Auth.InvalidCredentials,
                "Invalid email/username or password");

        // Record login
        user.RecordLogin();

        // Generate tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.Email,
            user.Username);

        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshTokenHash = _jwtTokenService.HashRefreshToken(refreshToken);
        var refreshTokenExpiresAt = _jwtTokenService.GetRefreshTokenExpirationUtc();
        var accessTokenExpiresAt = _jwtTokenService.GetAccessTokenExpirationUtc();

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _refreshTokenRepository.StoreAsync(
            user.Id,
            refreshTokenHash,
            refreshTokenExpiresAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var payload = new LoginResponse(
            UserId: user.Id.Value,
            Email: user.Email,
            Username: user.Username,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: accessTokenExpiresAt
        );

        return ApplicationResponse<LoginResponse>.Ok(payload);
    }
}
