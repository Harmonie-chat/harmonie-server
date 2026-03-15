using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.GetMessages;

public sealed class GetMessagesHandler
{
    private const int DefaultLimit = 50;

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _conversationMessageRepository;
    private readonly ILogger<GetMessagesHandler> _logger;

    public GetMessagesHandler(
        IConversationRepository conversationRepository,
        IMessageRepository conversationMessageRepository,
        ILogger<GetMessagesHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _conversationMessageRepository = conversationMessageRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<GetMessagesResponse>> HandleAsync(
        ConversationId conversationId,
        GetMessagesRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetConversationMessages started. ConversationId={ConversationId}, UserId={UserId}, Limit={Limit}, HasCursor={HasCursor}",
            conversationId,
            currentUserId,
            request.Limit ?? DefaultLimit,
            request.Cursor is not null);

        MessageCursor? cursor = null;
        if (request.Cursor is not null)
        {
            if (!MessageCursorCodec.TryParse(request.Cursor, out var parsedCursor) || parsedCursor is null)
            {
                _logger.LogWarning(
                    "GetConversationMessages invalid cursor. ConversationId={ConversationId}, UserId={UserId}",
                    conversationId,
                    currentUserId);

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

        var conversation = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            _logger.LogWarning(
                "GetConversationMessages failed because conversation was not found. ConversationId={ConversationId}, UserId={UserId}",
                conversationId,
                currentUserId);

            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != currentUserId && conversation.User2Id != currentUserId)
        {
            _logger.LogWarning(
                "GetConversationMessages access denied. ConversationId={ConversationId}, UserId={UserId}",
                conversationId,
                currentUserId);

            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var page = await _conversationMessageRepository.GetConversationPageAsync(
            conversationId,
            cursor,
            limit,
            currentUserId,
            cancellationToken);

        _logger.LogInformation(
            "GetConversationMessages fetched page. ConversationId={ConversationId}, UserId={UserId}, ItemCount={ItemCount}, HasNextCursor={HasNextCursor}",
            conversationId,
            currentUserId,
            page.Items.Count,
            page.NextCursor is not null);

        var items = page.Items
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .Select(x =>
            {
                page.ReactionsByMessageId.TryGetValue(x.Id.Value, out var reactions);
                return new GetMessagesItemResponse(
                    MessageId: x.Id.ToString(),
                    AuthorUserId: x.AuthorUserId.ToString(),
                    Content: x.Content.Value,
                    Attachments: x.Attachments.Select(MessageAttachmentDto.FromDomain).ToArray(),
                    Reactions: reactions?.Select(r => new MessageReactionDto(r.Emoji, r.Count, r.ReactedByCaller)).ToArray()
                              ?? Array.Empty<MessageReactionDto>(),
                    CreatedAtUtc: x.CreatedAtUtc,
                    UpdatedAtUtc: x.UpdatedAtUtc);
            })
            .ToArray();

        var payload = new GetMessagesResponse(
            ConversationId: conversationId.ToString(),
            Items: items,
            NextCursor: page.NextCursor is null
                ? null
                : MessageCursorCodec.Encode(page.NextCursor));

        return ApplicationResponse<GetMessagesResponse>.Ok(payload);
    }
}
