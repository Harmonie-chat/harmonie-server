using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.GetMessages;

public sealed record GetConversationMessagesInput(ConversationId ConversationId, string? Cursor = null, int? Limit = null);

public sealed class GetMessagesHandler : IAuthenticatedHandler<GetConversationMessagesInput, GetMessagesResponse>
{
    private const int DefaultLimit = 50;

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessagePaginationRepository _conversationMessageRepository;
    private readonly ILinkPreviewRepository _linkPreviewRepository;

    public GetMessagesHandler(
        IConversationRepository conversationRepository,
        IMessagePaginationRepository conversationMessageRepository,
        ILinkPreviewRepository linkPreviewRepository)
    {
        _conversationRepository = conversationRepository;
        _conversationMessageRepository = conversationMessageRepository;
        _linkPreviewRepository = linkPreviewRepository;
    }

    public async Task<ApplicationResponse<GetMessagesResponse>> HandleAsync(
        GetConversationMessagesInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        MessageCursor? cursor = null;
        if (request.Cursor is not null)
        {
            if (!MessageCursorCodec.TryParse(request.Cursor, out var parsedCursor) || parsedCursor is null)
            {
                return ApplicationResponse<GetMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.Cursor),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Cursor is invalid"));
            }

            cursor = parsedCursor;
        }

        var limit = request.Limit ?? DefaultLimit;

        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(request.ConversationId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }
        if (access.Participant is null)
        {
            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var page = await _conversationMessageRepository.GetConversationPageAsync(
            request.ConversationId,
            cursor,
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
            ConversationId: request.ConversationId.Value,
            Items: items,
            NextCursor: page.NextCursor is null
                ? null
                : MessageCursorCodec.Encode(page.NextCursor),
            LastReadMessageId: page.LastReadState?.LastReadMessageId.Value,
            LastReadAtUtc: page.LastReadState?.ReadAtUtc);

        return ApplicationResponse<GetMessagesResponse>.Ok(payload);
    }
}
