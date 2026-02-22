using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.GetGuildChannels;

public sealed class GetGuildChannelsHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly ILogger<GetGuildChannelsHandler> _logger;

    public GetGuildChannelsHandler(
        IGuildRepository guildRepository,
        IGuildMemberRepository guildMemberRepository,
        IGuildChannelRepository guildChannelRepository,
        ILogger<GetGuildChannelsHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildMemberRepository = guildMemberRepository;
        _guildChannelRepository = guildChannelRepository;
        _logger = logger;
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

        _logger.LogInformation(
            "GetGuildChannels started. GuildId={GuildId}, RequesterUserId={RequesterUserId}",
            guildId,
            requesterUserId);

        var guild = await _guildRepository.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
        {
            _logger.LogWarning(
                "GetGuildChannels guild not found. GuildId={GuildId}, RequesterUserId={RequesterUserId}",
                guildId,
                requesterUserId);

            return ApplicationResponse<GetGuildChannelsResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        var isMember = await _guildMemberRepository.IsMemberAsync(
            guildId,
            requesterUserId,
            cancellationToken);
        if (!isMember)
        {
            _logger.LogWarning(
                "GetGuildChannels access denied. GuildId={GuildId}, RequesterUserId={RequesterUserId}",
                guildId,
                requesterUserId);

            return ApplicationResponse<GetGuildChannelsResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");
        }

        var channels = await _guildChannelRepository.GetByGuildIdAsync(guildId, cancellationToken);
        _logger.LogInformation(
            "GetGuildChannels succeeded. GuildId={GuildId}, RequesterUserId={RequesterUserId}, ChannelCount={ChannelCount}",
            guildId,
            requesterUserId,
            channels.Count);

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
