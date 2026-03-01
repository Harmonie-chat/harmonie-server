using FluentValidation;

namespace Harmonie.Application.Features.Channels.UpdateChannel;

public sealed class UpdateChannelRouteValidator : AbstractValidator<UpdateChannelRouteRequest>
{
    public UpdateChannelRouteValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty()
            .WithMessage("Channel ID is required")
            .Must(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Channel ID must be a valid non-empty GUID");
    }
}
