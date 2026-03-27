using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.GetGuildChannels;

public sealed class GetGuildChannelsHandler : IAuthenticatedHandler<GuildId, GetGuildChannelsResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;

    public GetGuildChannelsHandler(
        IGuildRepository guildRepository,
        IGuildChannelRepository guildChannelRepository)
    {
        _guildRepository = guildRepository;
        _guildChannelRepository = guildChannelRepository;
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

        var payload = new GetGuildChannelsResponse(
            GuildId: guildId.Value,
            Channels: channels.Select(channel => new GetGuildChannelsItemResponse(
                    ChannelId: channel.Id.Value,
                    Name: channel.Name,
                    Type: channel.Type.ToString(),
                    IsDefault: channel.IsDefault,
                    Position: channel.Position))
                .ToArray());

        return ApplicationResponse<GetGuildChannelsResponse>.Ok(payload);
    }
}
