using FluentValidation;

namespace Harmonie.Application.Features.Users.UpdateUserStatus;

public sealed class UpdateUserStatusValidator : AbstractValidator<UpdateUserStatusRequest>
{
    private static readonly string[] ValidStatuses = ["online", "idle", "dnd", "invisible"];

    public UpdateUserStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required");

        RuleFor(x => x.Status)
            .Must(status => ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status must be one of: online, idle, dnd, invisible")
            .When(x => !string.IsNullOrEmpty(x.Status));
    }
}
