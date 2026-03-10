using FluentValidation;

namespace Harmonie.Application.Features.Users.UpdateMyProfile;

public sealed class UpdateMyProfileValidator : AbstractValidator<UpdateMyProfileRequest>
{
    public UpdateMyProfileValidator()
    {
        RuleFor(x => x.DisplayName)
            .MaximumLength(100)
            .WithMessage("Display name cannot exceed 100 characters")
            .When(x => x.DisplayNameIsSet && x.DisplayName is not null);

        RuleFor(x => x.Bio)
            .MaximumLength(500)
            .WithMessage("Bio cannot exceed 500 characters")
            .When(x => x.BioIsSet && x.Bio is not null);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(2048)
            .WithMessage("Avatar URL cannot exceed 2048 characters")
            .When(x => x.AvatarUrlIsSet && x.AvatarUrl is not null);

        RuleFor(x => x.AvatarUrl)
            .Must(BeValidAbsoluteAvatarUrl)
            .WithMessage("Avatar URL must be a valid absolute HTTP or HTTPS URL")
            .When(x => x.AvatarUrlIsSet && x.AvatarUrl is not null);

        RuleFor(x => x.AvatarColor)
            .MaximumLength(50)
            .WithMessage("Avatar color cannot exceed 50 characters")
            .When(x => x.AvatarColorIsSet && x.AvatarColor is not null);

        RuleFor(x => x.AvatarIcon)
            .MaximumLength(50)
            .WithMessage("Avatar icon cannot exceed 50 characters")
            .When(x => x.AvatarIconIsSet && x.AvatarIcon is not null);

        RuleFor(x => x.AvatarBg)
            .MaximumLength(50)
            .WithMessage("Avatar background cannot exceed 50 characters")
            .When(x => x.AvatarBgIsSet && x.AvatarBg is not null);

        RuleFor(x => x.Theme)
            .MaximumLength(50)
            .WithMessage("Theme cannot exceed 50 characters")
            .When(x => x.ThemeIsSet && x.Theme is not null);

        RuleFor(x => x.Language)
            .MaximumLength(10)
            .WithMessage("Language cannot exceed 10 characters")
            .When(x => x.LanguageIsSet && x.Language is not null);
    }

    private static bool BeValidAbsoluteAvatarUrl(string? avatarUrl)
    {
        if (avatarUrl is null)
            return true;

        if (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }
}
