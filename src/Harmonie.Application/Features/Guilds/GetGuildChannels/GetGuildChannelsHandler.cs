using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Voice;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.GetGuildChannels;

public sealed class GetGuildChannelsHandler : IAuthenticatedHandler<GuildId, GetGuildChannelsResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IVoiceParticipantCache _voiceParticipantCache;

    public GetGuildChannelsHandler(
        IGuildRepository guildRepository,
        IGuildChannelRepository guildChannelRepository,
        IVoiceParticipantCache voiceParticipantCache)
    {
        _guildRepository = guildRepository;
        _guildChannelRepository = guildChannelRepository;
        _voiceParticipantCache = voiceParticipantCache;
    }

    public async Task<ApplicationResponse<GetGuildChannelsResponse>> HandleAsync(
        GuildId guildId,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<GetGuildChannelsResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<GetGuildChannelsResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");
        }

        var channels = await _guildChannelRepository.GetByGuildIdAsync(guildId, cancellationToken);

        var participantsByChannelId = new Dictionary<Guid, IReadOnlyList<CachedVoiceParticipant>>();
        foreach (var c in channels.Where(c => c.Type == GuildChannelType.Voice))
            participantsByChannelId[c.Id.Value] = await _voiceParticipantCache.GetAsync(c.Id, cancellationToken);

        var payload = new GetGuildChannelsResponse(
            GuildId: guildId.Value,
            Channels: channels.Select(channel =>
            {
                IReadOnlyList<GetGuildChannelsVoiceParticipantResponse>? currentParticipants = null;
                if (channel.Type == GuildChannelType.Voice && participantsByChannelId.TryGetValue(channel.Id.Value, out var cached))
                {
                    currentParticipants = cached
                        .Select(p => new GetGuildChannelsVoiceParticipantResponse(
                            UserId: p.UserId.Value,
                            Username: p.Username,
                            DisplayName: p.DisplayName,
                            AvatarFileId: p.AvatarFileId?.Value,
                            AvatarColor: p.AvatarColor,
                            AvatarIcon: p.AvatarIcon,
                            AvatarBg: p.AvatarBg))
                        .ToArray();
                }

                return new GetGuildChannelsItemResponse(
                    ChannelId: channel.Id.Value,
                    Name: channel.Name,
                    Type: channel.Type.ToString(),
                    IsDefault: channel.IsDefault,
                    Position: channel.Position,
                    CurrentParticipants: currentParticipants);
            }).ToArray());

        return ApplicationResponse<GetGuildChannelsResponse>.Ok(payload);
    }
}
