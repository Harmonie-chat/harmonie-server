using FluentValidation;

namespace Harmonie.Application.Features.Channels.UpdateChannel;

public sealed class UpdateChannelValidator : AbstractValidator<UpdateChannelRequest>
{
    public UpdateChannelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Channel name cannot be empty")
            .When(x => x.Name is not null);
    }
}
