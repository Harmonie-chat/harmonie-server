using FluentValidation;

namespace Harmonie.Application.Features.Guilds.TransferOwnership;

public sealed class TransferOwnershipValidator : AbstractValidator<TransferOwnershipRequest>
{
    public TransferOwnershipValidator()
    {
        RuleFor(x => x.NewOwnerId)
            .NotEmpty()
            .WithMessage("New owner ID is required")
            .Must(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty)
            .WithMessage("New owner ID must be a valid non-empty GUID");
    }
}
