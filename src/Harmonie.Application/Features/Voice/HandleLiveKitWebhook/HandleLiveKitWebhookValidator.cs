using FluentValidation;

namespace Harmonie.Application.Features.Voice.HandleLiveKitWebhook;

public sealed class HandleLiveKitWebhookValidator : AbstractValidator<HandleLiveKitWebhookRequest>
{
    public HandleLiveKitWebhookValidator()
    {
        RuleFor(x => x.RawBody)
            .NotEmpty()
            .WithMessage("Webhook body is required");

        RuleFor(x => x.AuthorizationHeader)
            .NotEmpty()
            .WithMessage("Authorization header is required");
    }
}
