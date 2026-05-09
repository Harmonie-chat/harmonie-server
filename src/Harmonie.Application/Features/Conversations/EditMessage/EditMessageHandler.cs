using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.Messages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Conversations.EditMessage;

public sealed record EditConversationMessageInput(ConversationId ConversationId, MessageId MessageId, string Content);

public sealed class EditMessageHandler : IAuthenticatedHandler<EditConversationMessageInput, EditMessageResponse>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IConversationMessageNotifier _conversationMessageNotifier;
    private readonly ILogger<ConversationMessageEditDeleteScope> _scopeLogger;
    private readonly MessageEditDeleteOrchestrator _orchestrator;

    public EditMessageHandler(
        IConversationRepository conversationRepository,
        IConversationMessageNotifier conversationMessageNotifier,
        ILogger<ConversationMessageEditDeleteScope> scopeLogger,
        MessageEditDeleteOrchestrator orchestrator)
    {
        _conversationRepository = conversationRepository;
        _conversationMessageNotifier = conversationMessageNotifier;
        _scopeLogger = scopeLogger;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<EditMessageResponse>> HandleAsync(
        EditConversationMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ConversationMessageEditDeleteScope(
            request.ConversationId,
            _conversationRepository,
            _conversationMessageNotifier,
            _scopeLogger);

        var result = await _orchestrator.EditAsync(
            scope,
            new MessageScope.Conversation(request.ConversationId),
            request.MessageId,
            request.Content,
            currentUserId,
            cancellationToken);

        if (!result.Success)
            return ApplicationResponse<EditMessageResponse>.Fail(result.Error);

        return ApplicationResponse<EditMessageResponse>.Ok(new EditMessageResponse(
            MessageId: result.Data.MessageId,
            ConversationId: request.ConversationId.Value,
            AuthorUserId: result.Data.AuthorUserId,
            Content: result.Data.Content,
            Attachments: result.Data.Attachments,
            CreatedAtUtc: result.Data.CreatedAtUtc,
            UpdatedAtUtc: result.Data.UpdatedAtUtc));
    }
}
