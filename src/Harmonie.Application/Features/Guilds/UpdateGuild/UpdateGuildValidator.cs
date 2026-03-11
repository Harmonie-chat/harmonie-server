using FluentValidation;
namespace Harmonie.Application.Features.Guilds.UpdateGuild;

public sealed class UpdateGuildValidator : AbstractValidator<UpdateGuildRequest>
{
    public UpdateGuildValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Guild name is required")
            .MinimumLength(3)
            .WithMessage("Guild name must be at least 3 characters")
            .MaximumLength(100)
            .WithMessage("Guild name cannot exceed 100 characters")
            .When(x => x.NameIsSet);

        RuleFor(x => x.IconFileId)
            .Must(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty)
            .WithMessage("Guild icon file ID must be a valid non-empty GUID")
            .When(x => x.IconFileIdIsSet && x.IconFileId is not null);

        RuleFor(x => x.IconColor)
            .MaximumLength(50)
            .WithMessage("Guild icon color cannot exceed 50 characters")
            .When(x => x.IconColorIsSet && x.IconColor is not null);

        RuleFor(x => x.IconName)
            .MaximumLength(50)
            .WithMessage("Guild icon name cannot exceed 50 characters")
            .When(x => x.IconNameIsSet && x.IconName is not null);

        RuleFor(x => x.IconBg)
            .MaximumLength(50)
            .WithMessage("Guild icon background cannot exceed 50 characters")
            .When(x => x.IconBgIsSet && x.IconBg is not null);
    }

}
