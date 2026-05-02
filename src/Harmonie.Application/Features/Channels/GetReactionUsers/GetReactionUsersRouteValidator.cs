using FluentValidation;

namespace Harmonie.Application.Features.Channels.GetReactionUsers;

public sealed class GetReactionUsersRouteValidator : AbstractValidator<GetReactionUsersRouteRequest>
{
    public GetReactionUsersRouteValidator()
    {
        RuleFor(x => x.Emoji)
            .NotEmpty()
            .MaximumLength(64);
    }
}
