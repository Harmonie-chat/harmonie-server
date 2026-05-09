using FluentValidation;

namespace Harmonie.Application.Features.Users.UpdateUserStatus;

public sealed class UpdateUserStatusValidator : AbstractValidator<UpdateUserStatusRequest>
{
    public UpdateUserStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required");
    }
}
