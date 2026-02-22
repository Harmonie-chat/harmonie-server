using Harmonie.Application.Interfaces;
using Harmonie.Domain.Exceptions;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Auth.Login;

/// <summary>
/// Handler for user login business logic
/// </summary>
public sealed class LoginHandler
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

    public async Task<LoginResponse> HandleAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // Try to find user by email or username
        var emailResult = Email.Create(request.EmailOrUsername);
        var usernameResult = Username.Create(request.EmailOrUsername);

        Harmonie.Domain.Entities.User? user = null;
        if (emailResult.IsSuccess && emailResult.Value is not null)
            user = await _userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);
        else if (usernameResult.IsSuccess && usernameResult.Value is not null)
            user = await _userRepository.GetByUsernameAsync(usernameResult.Value, cancellationToken);

        if (user == null)
            throw new InvalidPasswordException("Invalid email/username or password");

        if (!user.IsActive)
            throw new UserInactiveException(user.Id);

        // Verify password
        if (!_passwordHasher.VerifyPassword(user.Email, user.PasswordHash, request.Password))
            throw new InvalidPasswordException("Invalid email/username or password");

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

        return new LoginResponse(
            UserId: user.Id.ToString(),
            Email: user.Email,
            Username: user.Username,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: accessTokenExpiresAt
        );
    }
}
