using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IGuildBanRepository
{
    Task<bool> TryAddAsync(
        GuildBan ban,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        GuildId guildId,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuildBanWithUser>> GetByGuildIdAsync(
        GuildId guildId,
        CancellationToken cancellationToken = default);
}

public sealed record GuildBanWithUser(
    UserId UserId,
    Username Username,
    string? DisplayName,
    UploadedFileId? AvatarFileId,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg,
    string? Reason,
    UserId BannedBy,
    DateTime CreatedAtUtc);
