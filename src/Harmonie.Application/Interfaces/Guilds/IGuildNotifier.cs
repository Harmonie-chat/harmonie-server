using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Guilds;

public interface IGuildNotifier
{
    Task NotifyGuildDeletedAsync(
        GuildDeletedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyGuildOwnershipTransferredAsync(
        GuildOwnershipTransferredNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyChannelCreatedAsync(
        ChannelCreatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyChannelUpdatedAsync(
        ChannelUpdatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyChannelDeletedAsync(
        ChannelDeletedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyChannelsReorderedAsync(
        ChannelsReorderedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMemberJoinedAsync(
        MemberJoinedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMemberLeftAsync(
        MemberLeftNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record GuildDeletedNotification(
    GuildId GuildId);

public sealed record GuildOwnershipTransferredNotification(
    GuildId GuildId,
    UserId NewOwnerUserId);

public sealed record ChannelCreatedNotification(
    GuildId GuildId,
    GuildChannelId ChannelId,
    string Name,
    GuildChannelType Type,
    bool IsDefault,
    int Position);

public sealed record ChannelUpdatedNotification(
    GuildId GuildId,
    GuildChannelId ChannelId,
    string Name,
    int Position);

public sealed record ChannelDeletedNotification(
    GuildId GuildId,
    GuildChannelId ChannelId);

public sealed record ChannelsReorderedNotification(
    GuildId GuildId,
    IReadOnlyList<ChannelPositionItem> Channels);

public sealed record ChannelPositionItem(
    GuildChannelId ChannelId,
    int Position);

public sealed record MemberJoinedNotification(
    GuildId GuildId,
    UserId UserId,
    string? DisplayName,
    UploadedFileId? AvatarFileId);

public sealed record MemberLeftNotification(
    GuildId GuildId,
    UserId UserId);
