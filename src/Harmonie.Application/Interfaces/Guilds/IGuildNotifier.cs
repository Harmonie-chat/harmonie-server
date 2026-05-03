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

    Task NotifyMemberBannedAsync(
        MemberBannedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMemberRemovedAsync(
        MemberRemovedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyMemberRoleUpdatedAsync(
        MemberRoleUpdatedNotification notification,
        CancellationToken cancellationToken = default);

    Task NotifyGuildUpdatedAsync(
        GuildUpdatedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record GuildDeletedNotification(
    GuildId GuildId,
    string GuildName);

public sealed record GuildOwnershipTransferredNotification(
    GuildId GuildId,
    string GuildName,
    UserId NewOwnerUserId,
    string NewOwnerUsername,
    string? NewOwnerDisplayName);

public sealed record ChannelCreatedNotification(
    GuildId GuildId,
    string GuildName,
    GuildChannelId ChannelId,
    string Name,
    GuildChannelType Type,
    bool IsDefault,
    int Position);

public sealed record ChannelUpdatedNotification(
    GuildId GuildId,
    string GuildName,
    GuildChannelId ChannelId,
    string Name,
    int Position);

public sealed record ChannelDeletedNotification(
    GuildId GuildId,
    string GuildName,
    GuildChannelId ChannelId,
    string ChannelName);

public sealed record ChannelsReorderedNotification(
    GuildId GuildId,
    string GuildName,
    IReadOnlyList<ChannelPositionItem> Channels);

public sealed record ChannelPositionItem(
    GuildChannelId ChannelId,
    int Position);

public sealed record MemberJoinedNotification(
    GuildId GuildId,
    string GuildName,
    UserId UserId,
    string Username,
    string? DisplayName,
    UploadedFileId? AvatarFileId);

public sealed record MemberLeftNotification(
    GuildId GuildId,
    string GuildName,
    UserId UserId,
    string Username,
    string? DisplayName);

public sealed record MemberBannedNotification(
    GuildId GuildId,
    string GuildName,
    UserId BannedUserId,
    string Username,
    string? DisplayName);

public sealed record MemberRemovedNotification(
    GuildId GuildId,
    string GuildName,
    UserId RemovedUserId,
    string Username,
    string? DisplayName);

public sealed record MemberRoleUpdatedNotification(
    GuildId GuildId,
    string GuildName,
    UserId UserId,
    string Username,
    string? DisplayName,
    GuildRole NewRole);

public sealed record GuildUpdatedNotification(
    GuildId GuildId,
    string Name,
    UploadedFileId? IconFileId);
