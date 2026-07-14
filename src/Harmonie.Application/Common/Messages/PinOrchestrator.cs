using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common.Messages;

/// <summary>
/// Shared orchestrator for pin and unpin operations across all scopes.
/// </summary>
public sealed class PinOrchestrator
{
    private readonly IMessageRepository _messageRepository;
    private readonly IPinnedMessageRepository _pinnedMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public PinOrchestrator(
        IMessageRepository messageRepository,
        IPinnedMessageRepository pinnedMessageRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _messageRepository = messageRepository;
        _pinnedMessageRepository = pinnedMessageRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<ApplicationResponse<bool>> PinAsync<TContext>(
        IPinScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        // ── Authorization + message fetch ───────────────────────────────
        var fetched = await MessageScopeAuthorizer.AuthorizeAndFetchAsync(
            _messageRepository,
            (caller, ct) => scope.AuthorizeAsync(caller, ct),
            messageScope, messageId, callerId,
            ApplicationErrorCodes.Pin.MessageNotFound, ct);
        if (fetched is FetchMessageResult<TContext>.Failed failed)
            return ApplicationResponse<bool>.Fail(failed.Error);

        var (context, _) = (FetchMessageResult<TContext>.Found)fetched;

        // ── Create pin ──────────────────────────────────────────────────
        var pinnedMessage = PinnedMessage.Create(
            messageId,
            callerId,
            _timeProvider.GetUtcNow().UtcDateTime);
        if (pinnedMessage.IsFailure || pinnedMessage.Value is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                pinnedMessage.Error ?? "Invalid pin");
        }

        // ── Persist ─────────────────────────────────────────────────────
        await using var transaction = await _unitOfWork.BeginAsync(ct);
        await _pinnedMessageRepository.AddAsync(pinnedMessage.Value, ct);
        await transaction.CommitAsync(ct);

        // ── Notify ──────────────────────────────────────────────────────
        await scope.NotifyPinAddedAsync(context, messageId, callerId, pinnedMessage.Value.PinnedAtUtc, ct);

        return ApplicationResponse<bool>.Ok(true);
    }

    public async Task<ApplicationResponse<bool>> UnpinAsync<TContext>(
        IPinScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        // ── Authorization + message fetch ───────────────────────────────
        var fetched = await MessageScopeAuthorizer.AuthorizeAndFetchAsync(
            _messageRepository,
            (caller, ct) => scope.AuthorizeAsync(caller, ct),
            messageScope, messageId, callerId,
            ApplicationErrorCodes.Pin.MessageNotFound, ct);
        if (fetched is FetchMessageResult<TContext>.Failed failed)
            return ApplicationResponse<bool>.Fail(failed.Error);

        var (context, _) = (FetchMessageResult<TContext>.Found)fetched;

        // ── Persist ─────────────────────────────────────────────────────
        await using var transaction = await _unitOfWork.BeginAsync(ct);
        await _pinnedMessageRepository.RemoveAsync(messageId, ct);
        await transaction.CommitAsync(ct);

        // ── Notify ──────────────────────────────────────────────────────
        await scope.NotifyPinRemovedAsync(
            context,
            messageId,
            callerId,
            _timeProvider.GetUtcNow().UtcDateTime,
            ct);

        return ApplicationResponse<bool>.Ok(true);
    }
}
