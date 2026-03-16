using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public sealed record UserSubscriptions(
    IReadOnlyList<GuildId> GuildIds,
    IReadOnlyList<GuildChannelId> TextChannelIds,
    IReadOnlyList<ConversationId> ConversationIds);

public interface IUserSubscriptionRepository
{
    Task<UserSubscriptions> GetAllAsync(
        UserId userId,
        CancellationToken cancellationToken = default);
}
