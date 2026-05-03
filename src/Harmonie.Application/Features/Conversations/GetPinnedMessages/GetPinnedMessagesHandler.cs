using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.GetPinnedMessages;

public sealed record GetConversationPinnedMessagesInput(ConversationId ConversationId);

public sealed class GetPinnedMessagesHandler : IAuthenticatedHandler<GetConversationPinnedMessagesInput, GetConversationPinnedMessagesResponse>
{
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

        var pinnedMessages = await _pinnedMessageRepository.GetPinnedMessagesAsync(
            request.ConversationId,
            currentUserId,
            cancellationToken);

        var items = pinnedMessages
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
                Items: items));
    }
}
