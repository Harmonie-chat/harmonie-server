using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.GetPinnedMessages;

public sealed record GetConversationPinnedMessagesInput(ConversationId ConversationId, string? Before = null, int? Limit = null);

public sealed class GetPinnedMessagesHandler : IAuthenticatedHandler<GetConversationPinnedMessagesInput, GetConversationPinnedMessagesResponse>
{
    private const int DefaultLimit = 50;

    private readonly IConversationRepository _conversationRepository;
    private readonly IPinnedMessageRepository _pinnedMessageRepository;

    public GetPinnedMessagesHandler(
        IConversationRepository conversationRepository,
        IPinnedMessageRepository pinnedMessageRepository)
    {
        _conversationRepository = conversationRepository;
        _pinnedMessageRepository = pinnedMessageRepository;
    }

    public async Task<ApplicationResponse<GetConversationPinnedMessagesResponse>> HandleAsync(
        GetConversationPinnedMessagesInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        PinnedMessagesCursor? cursor = null;
        if (request.Before is not null)
        {
            if (!PinnedMessagesCursorCodec.TryParse(request.Before, out var parsed) || parsed is null)
            {
                return ApplicationResponse<GetConversationPinnedMessagesResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.Before),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Before cursor is invalid"));
            }

            cursor = parsed;
        }

        var limit = request.Limit ?? DefaultLimit;

        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(
            request.ConversationId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ApplicationResponse<GetConversationPinnedMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }
        if (access.Participant is null)
        {
            return ApplicationResponse<GetConversationPinnedMessagesResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var page = await _pinnedMessageRepository.GetPinnedMessagesAsync(
            request.ConversationId,
            currentUserId,
            cursor,
            limit,
            cancellationToken);

        var items = page.Items
            .Select(x => new GetPinnedMessagesItemResponse(
                MessageId: x.MessageId,
                AuthorUserId: x.AuthorUserId,
                Content: x.Content,
                Attachments: x.Attachments,
                Reactions: x.Reactions,
                LinkPreviews: x.LinkPreviews,
                CreatedAtUtc: x.CreatedAtUtc,
                UpdatedAtUtc: x.UpdatedAtUtc,
                PinnedByUserId: x.PinnedByUserId,
                PinnedAtUtc: x.PinnedAtUtc))
            .ToArray();

        return ApplicationResponse<GetConversationPinnedMessagesResponse>.Ok(
            new GetConversationPinnedMessagesResponse(
                ConversationId: request.ConversationId.Value,
                Items: items,
                NextCursor: page.NextCursor is null
                    ? null
                    : PinnedMessagesCursorCodec.Encode(page.NextCursor)));
    }
}
