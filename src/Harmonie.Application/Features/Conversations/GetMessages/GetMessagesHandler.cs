using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.GetMessages;

public sealed record GetConversationMessagesInput(ConversationId ConversationId, string? Cursor = null, int? Limit = null);

public sealed class GetMessagesHandler : IAuthenticatedHandler<GetConversationMessagesInput, GetMessagesResponse>
{
    private const int DefaultLimit = 50;

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessagePaginationRepository _conversationMessageRepository;

    public GetMessagesHandler(
        IConversationRepository conversationRepository,
        IMessagePaginationRepository conversationMessageRepository)
    {
        _conversationRepository = conversationRepository;
        _conversationMessageRepository = conversationMessageRepository;
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

        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return ApplicationResponse<GetMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }

        var isParticipant = await _conversationRepository.IsParticipantAsync(request.ConversationId, currentUserId, cancellationToken);
        if (!isParticipant)
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
            ConversationId: request.ConversationId.ToString(),
            Items: items,
            NextCursor: page.NextCursor is null
                ? null
                : MessageCursorCodec.Encode(page.NextCursor),
            LastReadMessageId: page.LastReadMessageId?.ToString());

        return ApplicationResponse<GetMessagesResponse>.Ok(payload);
    }
}
