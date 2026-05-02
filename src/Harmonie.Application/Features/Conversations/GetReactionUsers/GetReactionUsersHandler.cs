using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Conversations.GetReactionUsers;

public sealed record GetConversationReactionUsersInput(
    ConversationId ConversationId,
    MessageId MessageId,
    string Emoji,
    string? Cursor = null,
    int? Limit = null);

public sealed class GetReactionUsersHandler : IAuthenticatedHandler<GetConversationReactionUsersInput, GetReactionUsersResponse>
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageReactionRepository _reactionRepository;

    public GetReactionUsersHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IMessageReactionRepository reactionRepository)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _reactionRepository = reactionRepository;
    }

    public async Task<ApplicationResponse<GetReactionUsersResponse>> HandleAsync(
        GetConversationReactionUsersInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        ReactionUsersCursor? cursor = null;
        if (request.Cursor is not null)
        {
            if (!ReactionUsersCursorCodec.TryParse(request.Cursor, out var parsedCursor) || parsedCursor is null)
            {
                return ApplicationResponse<GetReactionUsersResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.Cursor),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Cursor is invalid"));
            }

            cursor = parsedCursor;
        }

        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);

        var access = await _conversationRepository.GetByIdWithParticipantCheckAsync(request.ConversationId, currentUserId, cancellationToken);
        if (access is null)
        {
            return ApplicationResponse<GetReactionUsersResponse>.Fail(
                ApplicationErrorCodes.Conversation.NotFound,
                "Conversation was not found");
        }
        if (access.Participant is null)
        {
            return ApplicationResponse<GetReactionUsersResponse>.Fail(
                ApplicationErrorCodes.Conversation.AccessDenied,
                "You do not have access to this conversation");
        }

        var message = await _messageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        var messageConversationId = message?.ConversationId;
        if (message is null || messageConversationId is null || messageConversationId != request.ConversationId)
        {
            return ApplicationResponse<GetReactionUsersResponse>.Fail(
                ApplicationErrorCodes.Reaction.MessageNotFound,
                "Message was not found");
        }

        var page = await _reactionRepository.GetReactionUsersAsync(
            request.MessageId,
            request.Emoji,
            limit,
            cursor,
            cancellationToken);

        var users = page.Users
            .Select(u => new ReactionUserDto(u.UserId, u.Username, u.DisplayName))
            .ToArray();

        var payload = new GetReactionUsersResponse(
            MessageId: request.MessageId.Value,
            Emoji: request.Emoji,
            TotalCount: page.TotalCount,
            Users: users,
            NextCursor: page.NextCursor is null ? null : ReactionUsersCursorCodec.Encode(page.NextCursor));

        return ApplicationResponse<GetReactionUsersResponse>.Ok(payload);
    }
}
