using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Exceptions;
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

    public async Task<RegisterResponse> HandleAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // Create value objects with validation
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure || emailResult.Value is null)
            throw new DomainValidationException(emailResult.Error ?? string.Empty);

        var usernameResult = Username.Create(request.Username);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new DomainValidationException(usernameResult.Error ?? string.Empty);

        // Check for duplicates
        if (await _userRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken))
            throw new DuplicateEmailException(emailResult.Value);

        if (await _userRepository.ExistsByUsernameAsync(usernameResult.Value, cancellationToken))
            throw new DuplicateUsernameException(usernameResult.Value);

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(emailResult.Value, request.Password);

        // Create user entity
        var userResult = User.Create(
            emailResult.Value,
            usernameResult.Value,
            passwordHash);

        if (userResult.IsFailure || userResult.Value is null)
            throw new InvalidOperationException(userResult.Error);

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

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _refreshTokenRepository.StoreAsync(
                user.Id,
                refreshTokenHash,
                refreshTokenExpiresAt,
                cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return new RegisterResponse(
            UserId: user.Id.ToString(),
            Email: user.Email,
            Username: user.Username,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: accessTokenExpiresAt
        );
    }
}
