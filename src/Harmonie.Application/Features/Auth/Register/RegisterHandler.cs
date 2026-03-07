using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Auth.Register;

/// <summary>
/// Handler for user registration business logic
/// </summary>
public sealed class RegisterHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public RegisterHandler(
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

    public async Task<ApplicationResponse<RegisterResponse>> HandleAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // Create value objects with validation
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure || emailResult.Value is null)
        {
            var details = new Dictionary<string, string[]>
            {
                [nameof(request.Email)] = [emailResult.Error ?? "Email format is invalid"]
            };

            return ApplicationResponse<RegisterResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                details);
        }

        var usernameResult = Username.Create(request.Username);
        if (usernameResult.IsFailure || usernameResult.Value is null)
        {
            var details = new Dictionary<string, string[]>
            {
                [nameof(request.Username)] = [usernameResult.Error ?? "Username format is invalid"]
            };

            return ApplicationResponse<RegisterResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                details);
        }

        // Check for duplicates
        var duplicates = await _userRepository.CheckDuplicatesAsync(emailResult.Value, usernameResult.Value, cancellationToken);

        if (duplicates.EmailExists)
            return ApplicationResponse<RegisterResponse>.Fail(
                ApplicationErrorCodes.Auth.DuplicateEmail,
                $"A user with email '{emailResult.Value}' already exists");

        if (duplicates.UsernameExists)
            return ApplicationResponse<RegisterResponse>.Fail(
                ApplicationErrorCodes.Auth.DuplicateUsername,
                $"A user with username '{usernameResult.Value}' already exists");

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(emailResult.Value, request.Password);

        // Create user entity
        var userResult = User.Create(
            emailResult.Value,
            usernameResult.Value,
            passwordHash);

        if (userResult.IsFailure || userResult.Value is null)
            return ApplicationResponse<RegisterResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                userResult.Error ?? "Unable to create user");

        var user = userResult.Value;

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
        await _userRepository.AddAsync(user, cancellationToken);
        await _refreshTokenRepository.StoreAsync(
            user.Id,
            refreshTokenHash,
            refreshTokenExpiresAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var payload = new RegisterResponse(
            UserId: user.Id.ToString(),
            Email: user.Email,
            Username: user.Username,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: accessTokenExpiresAt
        );

        return ApplicationResponse<RegisterResponse>.Ok(payload);
    }
}
