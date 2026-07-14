using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common.Messages;

public sealed class ReactionOrchestrator
{
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageReactionRepository _reactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ReactionOrchestrator(
        IMessageRepository messageRepository,
        IMessageReactionRepository reactionRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _messageRepository = messageRepository;
        _reactionRepository = reactionRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<ApplicationResponse<bool>> AddAsync<TContext>(
        IReactionScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        string emoji,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        var authResult = await scope.AuthorizeAsync(callerId, ct);
        if (authResult is AuthorizationResult<TContext>.Denied denied)
            return ApplicationResponse<bool>.Fail(denied.Error);

        var context = ((AuthorizationResult<TContext>.Authorized)authResult).Context;

        var message = await _messageRepository.GetByIdAsync(messageId, ct);
        if (message is null || message.Scope != messageScope)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Reaction.MessageNotFound,
                "Message was not found");
        }

        var reaction = MessageReaction.Create(
            messageId,
            callerId,
            emoji,
            _timeProvider.GetUtcNow().UtcDateTime);
        if (reaction.IsFailure || reaction.Value is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                reaction.Error ?? "Invalid reaction");
        }

        await using var transaction = await _unitOfWork.BeginAsync(ct);
        await _reactionRepository.AddAsync(reaction.Value, ct);
        await transaction.CommitAsync(ct);

        await scope.NotifyReactionAddedAsync(context, messageId, callerId, emoji, ct);

        return ApplicationResponse<bool>.Ok(true);
    }

    public async Task<ApplicationResponse<bool>> RemoveAsync<TContext>(
        IReactionScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        string emoji,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        var authResult = await scope.AuthorizeAsync(callerId, ct);
        if (authResult is AuthorizationResult<TContext>.Denied denied)
            return ApplicationResponse<bool>.Fail(denied.Error);

        var context = ((AuthorizationResult<TContext>.Authorized)authResult).Context;

        var message = await _messageRepository.GetByIdAsync(messageId, ct);
        if (message is null || message.Scope != messageScope)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Reaction.MessageNotFound,
                "Message was not found");
        }

        await using var transaction = await _unitOfWork.BeginAsync(ct);
        await _reactionRepository.RemoveAsync(messageId, callerId, emoji, ct);
        await transaction.CommitAsync(ct);

        await scope.NotifyReactionRemovedAsync(context, messageId, callerId, emoji, ct);

        return ApplicationResponse<bool>.Ok(true);
    }

    public async Task<ApplicationResponse<ReactionUsersResult>> GetUsersAsync<TContext>(
        IReactionScope<TContext> scope,
        MessageScope messageScope,
        MessageId messageId,
        string emoji,
        string? rawCursor,
        int? rawLimit,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        var authResult = await scope.AuthorizeAsync(callerId, ct);
        if (authResult is AuthorizationResult<TContext>.Denied denied)
            return ApplicationResponse<ReactionUsersResult>.Fail(denied.Error);

        ReactionUsersCursor? cursor = null;
        if (rawCursor is not null)
        {
            if (!ReactionUsersCursorCodec.TryParse(rawCursor, out var parsedCursor) || parsedCursor is null)
            {
                return ApplicationResponse<ReactionUsersResult>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(rawCursor),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Cursor is invalid"));
            }
            cursor = parsedCursor;
        }

        var limit = Math.Clamp(rawLimit ?? 50, 1, 100);

        var message = await _messageRepository.GetByIdAsync(messageId, ct);
        if (message is null || message.Scope != messageScope)
        {
            return ApplicationResponse<ReactionUsersResult>.Fail(
                ApplicationErrorCodes.Reaction.MessageNotFound,
                "Message was not found");
        }

        var page = await _reactionRepository.GetReactionUsersAsync(
            messageId, emoji, limit, cursor, ct);

        var users = page.Users
            .Select(u => new ReactionUserDto(u.UserId, u.Username, u.DisplayName))
            .ToArray();

        return ApplicationResponse<ReactionUsersResult>.Ok(new ReactionUsersResult(
            MessageId: messageId.Value,
            Emoji: emoji,
            TotalCount: page.TotalCount,
            Users: users,
            NextCursor: page.NextCursor is null ? null : ReactionUsersCursorCodec.Encode(page.NextCursor)));
    }
}
