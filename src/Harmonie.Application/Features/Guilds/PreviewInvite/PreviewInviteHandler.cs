using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Guilds;

namespace Harmonie.Application.Features.Guilds.PreviewInvite;

public sealed class PreviewInviteHandler : IHandler<string, PreviewInviteResponse>
{
    private readonly IGuildInviteRepository _guildInviteRepository;

    public PreviewInviteHandler(
        IGuildInviteRepository guildInviteRepository)
    {
        _guildInviteRepository = guildInviteRepository;
    }

    public async Task<ApplicationResponse<PreviewInviteResponse>> HandleAsync(
        string inviteCode,
        CancellationToken cancellationToken = default)
    {
        var preview = await _guildInviteRepository.GetPreviewByCodeAsync(inviteCode, cancellationToken);
        if (preview is null)
        {
            return ApplicationResponse<PreviewInviteResponse>.Fail(
                ApplicationErrorCodes.Invite.NotFound,
                "Invite was not found");
        }

        if (preview.ExpiresAtUtc.HasValue && preview.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            return ApplicationResponse<PreviewInviteResponse>.Fail(
                ApplicationErrorCodes.Invite.Expired,
                "This invite has expired");
        }

        if (preview.MaxUses.HasValue && preview.UsesCount >= preview.MaxUses.Value)
        {
            return ApplicationResponse<PreviewInviteResponse>.Fail(
                ApplicationErrorCodes.Invite.Exhausted,
                "This invite has reached its maximum number of uses");
        }

        GuildIconDto? guildIcon = null;
        if (preview.GuildIconColor is not null || preview.GuildIconName is not null || preview.GuildIconBg is not null)
        {
            guildIcon = new GuildIconDto(preview.GuildIconColor, preview.GuildIconName, preview.GuildIconBg);
        }

        var payload = new PreviewInviteResponse(
            GuildName: preview.GuildName,
            GuildIconFileId: preview.GuildIconFileId?.ToString(),
            GuildIcon: guildIcon,
            MemberCount: preview.MemberCount,
            UsesCount: preview.UsesCount,
            MaxUses: preview.MaxUses,
            ExpiresAtUtc: preview.ExpiresAtUtc);

        return ApplicationResponse<PreviewInviteResponse>.Ok(payload);
    }
}
