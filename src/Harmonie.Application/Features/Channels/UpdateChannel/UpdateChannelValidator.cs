using FluentValidation;

namespace Harmonie.Application.Features.Channels.UpdateChannel;

public sealed class UpdateChannelValidator : AbstractValidator<UpdateChannelRequest>
{
    public UpdateChannelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Channel name cannot be empty")
            .MaximumLength(100)
            .WithMessage("Channel name cannot exceed 100 characters")
            .When(x => x.Name is not null);

        RuleFor(x => x.Position)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Channel position cannot be negative")
            .When(x => x.Position is not null);
    }
}
