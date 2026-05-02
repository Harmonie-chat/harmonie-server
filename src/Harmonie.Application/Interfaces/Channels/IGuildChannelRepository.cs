using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Channels;

public sealed record ChannelAccessContext(
    GuildChannel Channel,
    GuildRole? CallerRole,
    string? CallerUsername = null,
    string? CallerDisplayName = null);

public sealed record ChannelParticipantProfile(
    Username Username,
    string? DisplayName,
    UploadedFileId? AvatarFileId,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg);

public sealed record ChannelWithParticipant(
    GuildChannel Channel,
    ChannelParticipantProfile? Participant);

public interface IGuildChannelRepository
{
    Task<GuildChannel?> GetByIdAsync(
        GuildChannelId channelId,
        CancellationToken cancellationToken = default);

    Task<ChannelWithParticipant?> GetWithParticipantAsync(
        GuildChannelId channelId,
        UserId participantId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        GuildChannel channel,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuildChannel>> GetByGuildIdAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        GuildChannel channel,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        GuildChannelId channelId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameInGuildAsync(
        GuildId guildId,
        string name,
        GuildChannelId excludeId,
        CancellationToken cancellationToken = default);

    Task<ChannelAccessContext?> GetWithCallerRoleAsync(
        GuildChannelId channelId,
        UserId callerId,
        CancellationToken cancellationToken = default);
}
