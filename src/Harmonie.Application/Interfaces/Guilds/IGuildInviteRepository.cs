using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Guilds;

public interface IGuildInviteRepository
{
    /// <summary>
    /// Add the invite; returns false when the generated code collides with an
    /// existing one so the caller can retry with a fresh code.
    /// </summary>
    Task<bool> TryAddAsync(GuildInvite invite, CancellationToken cancellationToken = default);
    Task<InvitePreview?> GetPreviewByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<InviteAcceptDetails?> GetAcceptDetailsByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consume one invite use; returns false when the invite has
    /// already reached its max_uses limit.
    /// </summary>
    Task<bool> TryIncrementUsesCountAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GuildInviteSummary>> GetByGuildIdAsync(GuildId guildId, CancellationToken cancellationToken = default);
    Task<InviteRevokeDetails?> GetRevokeDetailsByCodeAsync(GuildId guildId, string code, CancellationToken cancellationToken = default);
    Task RevokeAsync(string code, DateTime revokedAtUtc, CancellationToken cancellationToken = default);
}

public sealed record InvitePreview(
    string Code,
    string GuildName,
    UploadedFileId? GuildIconFileId,
    string? GuildIconColor,
    string? GuildIconName,
    string? GuildIconBg,
    int MemberCount,
    int UsesCount,
    int? MaxUses,
    DateTime? ExpiresAtUtc);

public sealed record InviteAcceptDetails(
    GuildId GuildId,
    UserId CreatorId,
    int UsesCount,
    int? MaxUses,
    DateTime? ExpiresAtUtc);

public sealed record GuildInviteSummary(
    string Code,
    UserId CreatorId,
    int UsesCount,
    int? MaxUses,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc);

public sealed record InviteRevokeDetails(
    UserId CreatorId);
