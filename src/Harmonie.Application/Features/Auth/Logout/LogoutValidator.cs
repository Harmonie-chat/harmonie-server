using FluentValidation;

namespace Harmonie.Application.Features.Auth.Logout;

/// <summary>
/// Validator for LogoutRequest.
/// </summary>
public sealed class LogoutValidator : AbstractValidator<LogoutRequest>
{
    public LogoutValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required");
    }
}
