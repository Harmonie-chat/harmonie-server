using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Channels.GetMessages;

public sealed class GetMessagesHandler
{
    private const int DefaultLimit = 50;

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IChannelMessageRepository _channelMessageRepository;

    public GetMessagesHandler(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        IChannelMessageRepository channelMessageRepository)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _channelMessageRepository = channelMessageRepository;
    }

    public async Task<ApplicationResponse<GetMessagesResponse>> HandleAsync(
        GuildChannelId channelId,
        GetMessagesRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (channelId is null)
            throw new ArgumentNullException(nameof(channelId));
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (currentUserId is null)
            throw new ArgumentNullException(nameof(currentUserId));

        ChannelMessageCursor? beforeCursor = null;
        if (request.Before is not null)
        {
            if (!ChannelMessageCursorCodec.TryParse(request.Before, out var parsedCursor) || parsedCursor is null)
            {
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
            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (channel.Type != GuildChannelType.Text)
        {
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
            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var page = await _channelMessageRepository.GetPageAsync(
            channelId,
            beforeCursor,
            limit,
            cancellationToken);

        var items = page.Items
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .Select(x => new GetMessagesItemResponse(
                MessageId: x.Id.ToString(),
                AuthorUserId: x.AuthorUserId.ToString(),
                Content: x.Content.Value,
                CreatedAtUtc: x.CreatedAtUtc))
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
