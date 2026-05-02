using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.GetMessages;

public sealed record GetChannelMessagesInput(GuildChannelId ChannelId, string? Before = null, int? Limit = null);

public sealed class GetMessagesHandler : IAuthenticatedHandler<GetChannelMessagesInput, GetMessagesResponse>
{
    private const int DefaultLimit = 50;

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessagePaginationRepository _channelMessageRepository;
    private readonly ILinkPreviewRepository _linkPreviewRepository;

    public GetMessagesHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessagePaginationRepository channelMessageRepository,
        ILinkPreviewRepository linkPreviewRepository)
    {
        _guildChannelRepository = guildChannelRepository;
        _channelMessageRepository = channelMessageRepository;
        _linkPreviewRepository = linkPreviewRepository;
    }

    public async Task<ApplicationResponse<GetMessagesResponse>> HandleAsync(
        GetChannelMessagesInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        MessageCursor? beforeCursor = null;
        if (request.Before is not null)
        {
            if (!MessageCursorCodec.TryParse(request.Before, out var parsedCursor) || parsedCursor is null)
            {
                return ApplicationResponse<GetMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.Before),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Before cursor is invalid"));
            }

            beforeCursor = parsedCursor;
        }

        var limit = request.Limit ?? DefaultLimit;

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request.ChannelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be read from text channels");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var page = await _channelMessageRepository.GetChannelPageAsync(
            request.ChannelId,
            beforeCursor,
            limit,
            currentUserId,
            cancellationToken);

        var messageIds = page.Items.Select(x => x.Id).ToArray();
        IReadOnlyList<MessageLinkPreview> linkPreviews = messageIds.Length > 0
            ? await _linkPreviewRepository.GetByMessageIdsAsync(messageIds, cancellationToken)
            : Array.Empty<MessageLinkPreview>();
        var linkPreviewsByMessageId = linkPreviews
            .GroupBy(p => p.MessageId.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MessageLinkPreview>)g.ToArray());

        var items = page.Items
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .Select(x =>
            {
                page.ReactionsByMessageId.TryGetValue(x.Id.Value, out var reactions);
                linkPreviewsByMessageId.TryGetValue(x.Id.Value, out var previews);
                return new GetMessagesItemResponse(
                    MessageId: x.Id.Value,
                    AuthorUserId: x.AuthorUserId.Value,
                    Content: x.Content?.Value,
                    Attachments: x.Attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
                    Reactions: reactions?.Select(r => new MessageReactionDto(r.Emoji, r.Count, r.ReactedByCaller,
                        r.Users.Select(u => new ReactionUserDto(u.UserId, u.Username, u.DisplayName)).ToArray())).ToArray()
                              ?? Array.Empty<MessageReactionDto>(),
                    LinkPreviews: previews?.Select(p => new LinkPreviewDto(p.Url, p.Title, p.Description, p.ImageUrl, p.SiteName)).ToArray(),
                    CreatedAtUtc: x.CreatedAtUtc,
                    UpdatedAtUtc: x.UpdatedAtUtc);
            })
            .ToArray();

        var payload = new GetMessagesResponse(
            ChannelId: request.ChannelId.Value,
            Items: items,
            NextCursor: page.NextCursor is null
                ? null
                : MessageCursorCodec.Encode(page.NextCursor),
            LastReadMessageId: page.LastReadState?.LastReadMessageId.Value,
            LastReadAtUtc: page.LastReadState?.ReadAtUtc);

        return ApplicationResponse<GetMessagesResponse>.Ok(payload);
    }
}
