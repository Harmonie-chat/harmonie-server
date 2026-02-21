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
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
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

        var user = emailResult.IsSuccess
            ? await _userRepository.GetByEmailAsync(emailResult.Value!, cancellationToken)
            : usernameResult.IsSuccess
                ? await _userRepository.GetByUsernameAsync(usernameResult.Value!, cancellationToken)
                : null;

        if (user == null)
            throw new InvalidPasswordException("Invalid email/username or password");

        if (!user.IsActive)
            throw new UserInactiveException(user.Id);

        // Verify password
        if (!_passwordHasher.VerifyPassword(user.Email, user.PasswordHash, request.Password))
            throw new InvalidPasswordException("Invalid email/username or password");

        // Record login
        user.RecordLogin();
        await _userRepository.UpdateAsync(user, cancellationToken);

        // Generate tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.Email,
            user.Username);

        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        // TODO: Store refresh token in database with expiration

        return new LoginResponse(
            UserId: user.Id.ToString(),
            Email: user.Email,
            Username: user.Username,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(15)
        );
    }
}
