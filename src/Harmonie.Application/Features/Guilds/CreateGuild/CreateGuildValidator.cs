using FluentValidation;

namespace Harmonie.Application.Features.Guilds.CreateGuild;

public sealed class CreateGuildValidator : AbstractValidator<CreateGuildRequest>
{
    public CreateGuildValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Guild name is required")
            .MinimumLength(3)
            .WithMessage("Guild name must be at least 3 characters")
            .MaximumLength(100)
            .WithMessage("Guild name cannot exceed 100 characters");
    }
}
