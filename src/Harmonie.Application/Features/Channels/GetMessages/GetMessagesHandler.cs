using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.GetMessages;

public sealed class GetMessagesHandler
{
    private const int DefaultLimit = 50;

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IChannelMessageRepository _channelMessageRepository;
    private readonly ILogger<GetMessagesHandler> _logger;

    public GetMessagesHandler(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        IChannelMessageRepository channelMessageRepository,
        ILogger<GetMessagesHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _channelMessageRepository = channelMessageRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<GetMessagesResponse>> HandleAsync(
        GuildChannelId channelId,
        GetMessagesRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetMessages started. ChannelId={ChannelId}, UserId={UserId}, Limit={Limit}, HasBefore={HasBefore}",
            channelId,
            currentUserId,
            request.Limit ?? DefaultLimit,
            request.Before is not null);

        ChannelMessageCursor? beforeCursor = null;
        if (request.Before is not null)
        {
            if (!ChannelMessageCursorCodec.TryParse(request.Before, out var parsedCursor) || parsedCursor is null)
            {
                _logger.LogWarning(
                    "GetMessages invalid cursor. ChannelId={ChannelId}, UserId={UserId}",
                    channelId,
                    currentUserId);

                var details = new Dictionary<string, string[]>
                {
                    [nameof(request.Before)] = ["Before cursor is invalid"]
                };

                return ApplicationResponse<GetMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    details);
            }

            beforeCursor = parsedCursor;
        }

        var limit = request.Limit ?? DefaultLimit;

        var channel = await _guildChannelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel is null)
        {
            _logger.LogWarning(
                "GetMessages failed because channel was not found. ChannelId={ChannelId}, UserId={UserId}",
                channelId,
                currentUserId);

            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (channel.Type != GuildChannelType.Text)
        {
            _logger.LogWarning(
                "GetMessages failed because channel is not text. ChannelId={ChannelId}, ChannelType={ChannelType}, UserId={UserId}",
                channelId,
                channel.Type,
                currentUserId);

            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be read from text channels");
        }

        var isMember = await _guildMemberRepository.IsMemberAsync(
            channel.GuildId,
            currentUserId,
            cancellationToken);
        if (!isMember)
        {
            _logger.LogWarning(
                "GetMessages access denied. ChannelId={ChannelId}, GuildId={GuildId}, UserId={UserId}",
                channelId,
                channel.GuildId,
                currentUserId);

            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var page = await _channelMessageRepository.GetPageAsync(
            channelId,
            beforeCursor,
            limit,
            cancellationToken);

        _logger.LogInformation(
            "GetMessages fetched page. ChannelId={ChannelId}, UserId={UserId}, ItemCount={ItemCount}, HasNextCursor={HasNextCursor}",
            channelId,
            currentUserId,
            page.Items.Count,
            page.NextCursor is not null);

        var items = page.Items
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .Select(x => new GetMessagesItemResponse(
                MessageId: x.Id.ToString(),
                AuthorUserId: x.AuthorUserId.ToString(),
                Content: x.Content.Value,
                CreatedAtUtc: x.CreatedAtUtc,
                UpdatedAtUtc: x.UpdatedAtUtc))
            .ToArray();

        var payload = new GetMessagesResponse(
            ChannelId: channelId.ToString(),
            Items: items,
            NextCursor: page.NextCursor is null
                ? null
                : ChannelMessageCursorCodec.Encode(page.NextCursor));

        return ApplicationResponse<GetMessagesResponse>.Ok(payload);
    }
}
