using Harmonie.Application.Common;
using Harmonie.Application.Common.Auth;
using Harmonie.Application.Features.Users;
using Harmonie.Application.Interfaces.Auth;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Common;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Auth.Register;

/// <summary>
/// Handler for user registration business logic
/// </summary>
public sealed class RegisterHandler : IHandler<RegisterRequest, AuthSessionResult<RegisterResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly TimeProvider _timeProvider;

    public RegisterHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _timeProvider = timeProvider;
    }

    public async Task<ApplicationResponse<AuthSessionResult<RegisterResponse>>> HandleAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // Create value objects with validation
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure || emailResult.Value is null)
        {
            return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Fail(
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
            return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Fail(
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
            return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Fail(
                ApplicationErrorCodes.Auth.DuplicateEmail,
                $"A user with email '{emailResult.Value}' already exists");

        if (duplicates.UsernameExists)
            return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Fail(
                ApplicationErrorCodes.Auth.DuplicateUsername,
                $"A user with username '{usernameResult.Value}' already exists");

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(emailResult.Value, request.Password);

        // Create user entity
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var userResult = User.Create(
            emailResult.Value,
            usernameResult.Value,
            passwordHash,
            nowUtc);

        if (userResult.IsFailure || userResult.Value is null)
            return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                userResult.Error ?? "Unable to create user");

        var user = userResult.Value;

        // Apply optional avatar fields
        if (request.Avatar is not null)
        {
            var appearanceResult = Appearance.Create(request.Avatar.Color, request.Avatar.Icon, request.Avatar.Bg);
            if (appearanceResult.IsFailure || appearanceResult.Value is null)
                return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        "Avatar",
                        ApplicationErrorCodes.Validation.Invalid,
                        appearanceResult.Error ?? "Avatar appearance is invalid"));

            user.UpdateAvatar(appearanceResult.Value, nowUtc);
        }

        // Apply optional theme
        if (request.Theme is not null)
        {
            var themeResult = user.UpdateTheme(request.Theme, nowUtc);
            if (themeResult.IsFailure)
                return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Fail(
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

        // The duplicate pre-check above can race with a concurrent registration;
        // the unique constraints are the source of truth at insert time.
        var addResult = await _userRepository.TryAddAsync(user, cancellationToken);

        if (addResult == UserAddResult.DuplicateEmail)
            return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Fail(
                ApplicationErrorCodes.Auth.DuplicateEmail,
                $"A user with email '{emailResult.Value}' already exists");

        if (addResult == UserAddResult.DuplicateUsername)
            return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Fail(
                ApplicationErrorCodes.Auth.DuplicateUsername,
                $"A user with username '{usernameResult.Value}' already exists");

        await _refreshTokenRepository.StoreAsync(
            user.Id,
            refreshTokenHash,
            refreshTokenExpiresAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var avatar = user.Avatar.HasValue
            ? new AvatarAppearanceDto(user.Avatar.Color, user.Avatar.Glyph, user.Avatar.Bg)
            : null;

        var response = new RegisterResponse(
            UserId: user.Id.Value,
            Email: user.Email,
            Username: user.Username,
            AccessToken: accessToken,
            ExpiresAt: accessTokenExpiresAt,
            Avatar: avatar,
            Theme: user.Theme);

        var result = new AuthSessionResult<RegisterResponse>(
            response,
            refreshToken,
            refreshTokenExpiresAt);

        return ApplicationResponse<AuthSessionResult<RegisterResponse>>.Ok(result);
    }
}
