using FluentValidation;

namespace Harmonie.Application.Features.Guilds.CreateChannel;

public sealed class CreateChannelValidator : AbstractValidator<CreateChannelRequest>
{
    public CreateChannelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Channel name is required")
            .MaximumLength(100)
            .WithMessage("Channel name cannot exceed 100 characters");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Channel type must be 'Text' or 'Voice'");

        RuleFor(x => x.Position)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Channel position cannot be negative");
    }
}
