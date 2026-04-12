using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Auth;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Auth.Register;

/// <summary>
/// Handler for user registration business logic
/// </summary>
public sealed class RegisterHandler : IHandler<RegisterRequest, RegisterResponse>
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
            return ApplicationResponse<RegisterResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.Email),
                    ApplicationErrorCodes.Validation.Email,
                    emailResult.Error ?? "Email format is invalid"));
        }

        var usernameResult = Username.Create(request.Username);
        if (usernameResult.IsFailure || usernameResult.Value is null)
        {
            return ApplicationResponse<RegisterResponse>.Fail(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                EndpointExtensions.SingleValidationError(
                    nameof(request.Username),
                    ApplicationErrorCodes.Validation.InvalidFormat,
                    usernameResult.Error ?? "Username format is invalid"));
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

        // Apply optional avatar fields
        if (request.Avatar is not null)
        {
            var colorResult = user.UpdateAvatarColor(request.Avatar.Color);
            if (colorResult.IsFailure)
                return ApplicationResponse<RegisterResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        "Avatar.Color",
                        ApplicationErrorCodes.Validation.Invalid,
                        colorResult.Error ?? "Avatar color is invalid"));

            var iconResult = user.UpdateAvatarIcon(request.Avatar.Icon);
            if (iconResult.IsFailure)
                return ApplicationResponse<RegisterResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        "Avatar.Icon",
                        ApplicationErrorCodes.Validation.Invalid,
                        iconResult.Error ?? "Avatar icon is invalid"));

            var bgResult = user.UpdateAvatarBg(request.Avatar.Bg);
            if (bgResult.IsFailure)
                return ApplicationResponse<RegisterResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        "Avatar.Bg",
                        ApplicationErrorCodes.Validation.Invalid,
                        bgResult.Error ?? "Avatar background is invalid"));
        }

        // Apply optional theme
        if (request.Theme is not null)
        {
            var themeResult = user.UpdateTheme(request.Theme);
            if (themeResult.IsFailure)
                return ApplicationResponse<RegisterResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.Theme),
                        ApplicationErrorCodes.Validation.Invalid,
                        themeResult.Error ?? "Theme is invalid"));
        }

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

        var avatar = user.AvatarColor is not null || user.AvatarIcon is not null || user.AvatarBg is not null
            ? new AvatarAppearanceDto(user.AvatarColor, user.AvatarIcon, user.AvatarBg)
            : null;

        var payload = new RegisterResponse(
            UserId: user.Id.Value,
            Email: user.Email,
            Username: user.Username,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: accessTokenExpiresAt,
            Avatar: avatar,
            Theme: user.Theme
        );

        return ApplicationResponse<RegisterResponse>.Ok(payload);
    }
}
