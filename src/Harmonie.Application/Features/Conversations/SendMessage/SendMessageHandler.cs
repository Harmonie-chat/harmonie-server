using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Services;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.SendMessage;

public sealed record SendConversationMessageInput(ConversationId ConversationId, string? Content, IReadOnlyList<Guid>? AttachmentFileIds = null, Guid? ReplyToMessageId = null, IReadOnlyList<Guid>? MentionedUserIds = null);

public sealed class SendMessageHandler : IAuthenticatedHandler<SendConversationMessageInput, SendMessageResponse>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationParticipantRepository _participantRepository;
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly LinkPreviewResolutionService _linkPreviewService;
    private readonly ILogger<ConversationSendMessageScope> _scopeLogger;
    private readonly MessageSendOrchestrator _orchestrator;

    public SendMessageHandler(
        IConversationRepository conversationRepository,
        IConversationParticipantRepository participantRepository,
        IConversationMessageNotifier conversationMessageNotifier,
        LinkPreviewResolutionService linkPreviewService,
        ILogger<ConversationSendMessageScope> scopeLogger,
        MessageSendOrchestrator orchestrator)
    {
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _conversationMessageNotifier = conversationMessageNotifier;
        _linkPreviewService = linkPreviewService;
        _scopeLogger = scopeLogger;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<SendMessageResponse>> HandleAsync(
        SendConversationMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ConversationSendMessageScope(
            request.ConversationId,
            _conversationRepository,
            _participantRepository,
            _conversationMessageNotifier,
            _linkPreviewService,
            _scopeLogger);

        var result = await _orchestrator.SendAsync(
            scope,
            new MessageScope.Conversation(request.ConversationId),
            request.Content,
            request.AttachmentFileIds,
            request.ReplyToMessageId,
            request.MentionedUserIds,
            currentUserId,
            cancellationToken);

        if (!result.Success)
            return ApplicationResponse<SendMessageResponse>.Fail(result.Error);

        return ApplicationResponse<SendMessageResponse>.Ok(new SendMessageResponse(
            result.Data.MessageId,
            request.ConversationId.Value,
            result.Data.AuthorUserId,
            result.Data.Content,
            result.Data.Attachments,
            result.Data.ReplyTo,
            result.Data.MentionedUserIds,
            result.Data.CreatedAtUtc));
    }
}
