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

        RuleFor(x => x.IconUrl)
            .MaximumLength(2048)
            .WithMessage("Guild icon URL cannot exceed 2048 characters")
            .When(x => x.IconUrl is not null);

        RuleFor(x => x.IconUrl)
            .Must(BeValidAbsoluteIconUrl)
            .WithMessage("Guild icon URL must be a valid absolute HTTP or HTTPS URL")
            .When(x => x.IconUrl is not null);

        RuleFor(x => x.Icon!.Color)
            .MaximumLength(50)
            .WithMessage("Guild icon color cannot exceed 50 characters")
            .When(x => x.Icon?.Color is not null);

        RuleFor(x => x.Icon!.Name)
            .MaximumLength(50)
            .WithMessage("Guild icon name cannot exceed 50 characters")
            .When(x => x.Icon?.Name is not null);

        RuleFor(x => x.Icon!.Bg)
            .MaximumLength(50)
            .WithMessage("Guild icon background cannot exceed 50 characters")
            .When(x => x.Icon?.Bg is not null);
    }

    private static bool BeValidAbsoluteIconUrl(string? iconUrl)
    {
        if (iconUrl is null)
            return true;

        if (!Uri.TryCreate(iconUrl, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }
}
