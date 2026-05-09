using Harmonie.Application.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common.Messages;

/// <summary>
/// Scope-specific concerns for message edit/delete operations (authorization and notification).
/// </summary>
public interface IMessageEditDeleteScope<TContext> where TContext : ScopeContext
{
    /// <summary>
    /// Authorizes the caller for the scope.
    /// Returns an error on failure, or a context on success.
    /// </summary>
    Task<AuthorizationResult<TContext>> AuthorizeAsync(UserId caller, CancellationToken ct);

    /// <summary>
    /// Whether the caller (given the authorized context) can delete messages they did not author.
    /// Returns true for channel admins, false for conversation contexts.
    /// </summary>
    bool CanDeleteOthersMessages(TContext context);

    /// <summary>
    /// Notifies scope participants that a message was updated.
    /// Implementation must use best-effort notification (fire-and-forget).
    /// </summary>
    Task NotifyMessageUpdatedAsync(
        TContext context,
        MessageId messageId,
        string? content,
        DateTime updatedAtUtc,
        CancellationToken ct);

    /// <summary>
    /// Notifies scope participants that a message was deleted.
    /// Implementation must use best-effort notification (fire-and-forget).
    /// </summary>
    Task NotifyMessageDeletedAsync(
        TContext context,
        MessageId messageId,
        CancellationToken ct);
}

/// <summary>
/// Result returned by <see cref="MessageEditDeleteOrchestrator.EditAsync"/>.
/// The caller maps this to the namespace-specific EditMessageResponse DTO.
/// </summary>
public sealed record MessageEditResult(
    Guid MessageId,
    Guid AuthorUserId,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Discriminated union for the result of <see cref="MessageEditDeleteOrchestrator"/>'s
/// internal authorization + message fetch step.
/// </summary>
public abstract record FetchMessageResult<TContext> where TContext : ScopeContext
{
    private FetchMessageResult() { }

    public sealed record Found(TContext Context, Message Message) : FetchMessageResult<TContext>;

    public sealed record Failed(ApplicationError Error) : FetchMessageResult<TContext>;
}
