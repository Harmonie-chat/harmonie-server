using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.DeleteMessageAttachment;

public sealed record DeleteConversationMessageAttachmentInput(ConversationId ConversationId, MessageId MessageId, UploadedFileId AttachmentId);

public sealed class DeleteMessageAttachmentHandler : IAuthenticatedHandler<DeleteConversationMessageAttachmentInput, bool>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationParticipantRepository _participantRepository;
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly ILogger<ConversationMessageEditDeleteScope> _scopeLogger;
    private readonly MessageEditDeleteOrchestrator _orchestrator;

    public DeleteMessageAttachmentHandler(
        IConversationRepository conversationRepository,
        IConversationParticipantRepository participantRepository,
        IConversationMessageNotifier conversationMessageNotifier,
        ILogger<ConversationMessageEditDeleteScope> scopeLogger,
        MessageEditDeleteOrchestrator orchestrator)
    {
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _conversationMessageNotifier = conversationMessageNotifier;
        _scopeLogger = scopeLogger;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteConversationMessageAttachmentInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ConversationMessageEditDeleteScope(
            request.ConversationId,
            _conversationRepository,
            _participantRepository,
            _conversationMessageNotifier,
            _scopeLogger);

        return await _orchestrator.DeleteAttachmentAsync(
            scope,
            new MessageScope.Conversation(request.ConversationId),
            request.MessageId,
            request.AttachmentId,
            currentUserId,
            cancellationToken);
    }
}
