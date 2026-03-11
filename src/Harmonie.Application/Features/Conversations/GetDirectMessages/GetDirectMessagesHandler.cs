using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.GetDirectMessages;

public sealed class GetDirectMessagesHandler
{
    private const int DefaultLimit = 50;

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _directMessageRepository;
    private readonly ILogger<GetDirectMessagesHandler> _logger;

    public GetDirectMessagesHandler(
        IConversationRepository conversationRepository,
        IMessageRepository directMessageRepository,
        ILogger<GetDirectMessagesHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _directMessageRepository = directMessageRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<GetDirectMessagesResponse>> HandleAsync(
        ConversationId conversationId,
        GetDirectMessagesRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetDirectMessages started. ConversationId={ConversationId}, UserId={UserId}, Limit={Limit}, HasCursor={HasCursor}",
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
                    "GetDirectMessages invalid cursor. ConversationId={ConversationId}, UserId={UserId}",
                    conversationId,
                    currentUserId);

                return ApplicationResponse<GetDirectMessagesResponse>.Fail(
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
                "GetDirectMessages failed because conversation was not found. ConversationId={ConversationId}, UserId={UserId}",
                conversationId,
                currentUserId);

            return ApplicationResponse<GetDirectMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        if (conversation.User1Id != currentUserId && conversation.User2Id != currentUserId)
        {
            _logger.LogWarning(
                "GetDirectMessages access denied. ConversationId={ConversationId}, UserId={UserId}",
                conversationId,
                currentUserId);

            return ApplicationResponse<GetDirectMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var page = await _directMessageRepository.GetConversationPageAsync(
            conversationId,
            cursor,
            limit,
            cancellationToken);

        _logger.LogInformation(
            "GetDirectMessages fetched page. ConversationId={ConversationId}, UserId={UserId}, ItemCount={ItemCount}, HasNextCursor={HasNextCursor}",
            conversationId,
            currentUserId,
            page.Items.Count,
            page.NextCursor is not null);

        var items = page.Items
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .Select(x => new GetDirectMessagesItemResponse(
                MessageId: x.Id.ToString(),
                AuthorUserId: x.AuthorUserId.ToString(),
                Content: x.Content.Value,
                CreatedAtUtc: x.CreatedAtUtc,
                UpdatedAtUtc: x.UpdatedAtUtc))
            .ToArray();

        var payload = new GetDirectMessagesResponse(
            ConversationId: conversationId.ToString(),
            Items: items,
            NextCursor: page.NextCursor is null
                ? null
                : MessageCursorCodec.Encode(page.NextCursor));

        return ApplicationResponse<GetDirectMessagesResponse>.Ok(payload);
    }
}
