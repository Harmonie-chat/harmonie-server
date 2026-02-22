using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Guilds.GetGuildChannels;

public sealed class GetGuildChannelsHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;

    public GetGuildChannelsHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildChannelRepository guildChannelRepository)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildChannelRepository = guildChannelRepository;
    }

    public async Task<ApplicationResponse<GetGuildChannelsResponse>> HandleAsync(
        GuildId guildId,
        UserId requesterUserId,
        CancellationToken cancellationToken = default)
    {
        if (guildId is null)
            throw new ArgumentNullException(nameof(guildId));
        if (requesterUserId is null)
            throw new ArgumentNullException(nameof(requesterUserId));

        var guild = await _guildRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
            return ApplicationResponse<GetGuildChannelsResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");

        var isMember = await _guildMemberRepository.IsMemberAsync(
            guildId,
            requesterUserId,
            cancellationToken);
        if (!isMember)
            return ApplicationResponse<GetGuildChannelsResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");

        var channels = await _guildChannelRepository.GetByGuildIdAsync(guildId, cancellationToken);

        var payload = new GetGuildChannelsResponse(
            GuildId: guildId.ToString(),
            Channels: channels.Select(channel => new GetGuildChannelsItemResponse(
                    ChannelId: channel.Id.ToString(),
                    Name: channel.Name,
                    Type: channel.Type.ToString(),
                    IsDefault: channel.IsDefault,
                    Position: channel.Position))
                .ToArray());

        return ApplicationResponse<GetGuildChannelsResponse>.Ok(payload);
    }
}
