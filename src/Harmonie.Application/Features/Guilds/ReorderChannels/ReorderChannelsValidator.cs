using FluentValidation;

namespace Harmonie.Application.Features.Guilds.ReorderChannels;

public sealed class ReorderChannelsValidator : AbstractValidator<ReorderChannelsRequest>
{
    public ReorderChannelsValidator()
    {
        RuleFor(x => x.Channels)
            .NotNull()
            .WithMessage("Channels list is required")
            .NotEmpty()
            .WithMessage("Channels list cannot be empty");

        RuleForEach(x => x.Channels).ChildRules(channel =>
        {
            channel.RuleFor(c => c.ChannelId)
                .NotEmpty()
                .WithMessage("Channel ID is required")
                .Must(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty)
                .WithMessage("Channel ID must be a valid non-empty GUID");

            channel.RuleFor(c => c.Position)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Channel position cannot be negative");
        });
    }
}
