using FluentValidation;

namespace Harmonie.Application.Features.Guilds.CreateChannel;

public sealed class CreateChannelValidator : AbstractValidator<CreateChannelRequest>
{
    public CreateChannelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Channel name is required");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Channel type must be 'Text' or 'Voice'");
    }
}
