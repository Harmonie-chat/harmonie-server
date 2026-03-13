using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IGuildInviteRepository
{
    Task AddAsync(GuildInvite invite, CancellationToken cancellationToken = default);
    Task<InvitePreview?> GetPreviewByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GuildInviteSummary>> GetByGuildIdAsync(GuildId guildId, CancellationToken cancellationToken = default);
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

public sealed record GuildInviteSummary(
    string Code,
    UserId CreatorId,
    int UsesCount,
    int? MaxUses,
    DateTime? ExpiresAtUtc,
    DateTime CreatedAtUtc);
