using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common.Messages;

/// <summary>
/// Shared orchestrator for read acknowledgement operations across all scopes.
/// </summary>
public sealed class ReadOrchestrator
{
    private readonly IMessageRepository _messageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ReadOrchestrator(
        IMessageRepository messageRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _messageRepository = messageRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<ApplicationResponse<bool>> AcknowledgeAsync<TContext>(
        IReadScope<TContext> scope,
        MessageScope messageScope,
        MessageId? requestMessageId,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        // ── Authorization ───────────────────────────────────────────────
        var authResult = await scope.AuthorizeAsync(callerId, ct);
        if (authResult is AuthorizationResult<TContext>.Denied denied)
            return ApplicationResponse<bool>.Fail(denied.Error);

        // ── Resolve target message ──────────────────────────────────────
        MessageId resolvedMessageId;

        if (requestMessageId is not null)
        {
            var message = await _messageRepository.GetByIdAsync(requestMessageId, ct);
            if (message is null || message.Scope != messageScope)
            {
                return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Message.NotFound,
                    "Message was not found in this scope");
            }

            resolvedMessageId = requestMessageId;
        }
        else
        {
            var latestMessageId = await scope.GetLatestMessageIdAsync(ct);
            if (latestMessageId is null)
                return ApplicationResponse<bool>.Ok(true);

            resolvedMessageId = latestMessageId;
        }

        // ── Create read state ───────────────────────────────────────────
        var state = MessageReadState.Create(
            callerId,
            messageScope,
            resolvedMessageId,
            _timeProvider.GetUtcNow().UtcDateTime);
        if (state.IsFailure || state.Value is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                state.Error ?? "Invalid read state");
        }

        // ── Persist ─────────────────────────────────────────────────────
        await using var transaction = await _unitOfWork.BeginAsync(ct);
        await scope.UpsertReadStateAsync(state.Value, ct);
        await transaction.CommitAsync(ct);

        return ApplicationResponse<bool>.Ok(true);
    }
}
