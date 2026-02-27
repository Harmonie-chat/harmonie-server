using FluentValidation;

namespace Harmonie.Application.Features.Channels.GetMessages;

public sealed class GetMessagesRouteValidator : AbstractValidator<GetMessagesRouteRequest>
{
    public GetMessagesRouteValidator()
    {
        RuleFor(x => x.ChannelId)
            .NotEmpty()
            .WithMessage("Channel ID is required")
            .Must(channelId => Guid.TryParse(channelId, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Channel ID must be a valid non-empty GUID");
    }
}
