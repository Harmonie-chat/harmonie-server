using FluentValidation;

namespace Harmonie.Application.Features.Notifications.RegisterWebPushDevice;

public sealed class RegisterWebPushDeviceValidator : AbstractValidator<RegisterWebPushDeviceRequest>
{
    private static readonly long MaxUnixTimeMilliseconds = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    public RegisterWebPushDeviceValidator()
    {
        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .WithMessage("Endpoint is required")
            .Must(BeHttpsAbsoluteUri)
            .WithMessage("Endpoint must be an absolute HTTPS URI");

        RuleFor(x => x.ExpirationTime)
            .GreaterThan(0L)
            .WithMessage("ExpirationTime must be a positive Unix time in milliseconds")
            .LessThanOrEqualTo(MaxUnixTimeMilliseconds)
            .WithMessage("ExpirationTime is out of range")
            .When(x => x.ExpirationTime.HasValue);

        RuleFor(x => x.Keys)
            .NotNull()
            .WithMessage("Keys are required");

        When(x => x.Keys is not null, () =>
        {
            RuleFor(x => x.Keys.P256dh)
                .NotEmpty()
                .WithMessage("P256dh key is required");

            RuleFor(x => x.Keys.Auth)
                .NotEmpty()
                .WithMessage("Auth key is required");
        });
    }

    private static bool BeHttpsAbsoluteUri(string endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps;
}
