using Harmonie.Application.Common;
using Harmonie.Domain.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common.Messages;

/// <summary>
/// Opaque context returned by <see cref="ISendMessageScope{TContext}.AuthorizeAsync"/>
/// and consumed by downstream scope methods. Concrete subtypes are internal to
/// each scope implementation.
/// </summary>
public abstract record ScopeContext
{
    protected ScopeContext() { }
}

/// <summary>
/// Abstraction over scope-specific concerns for message operations
/// (authorization, notification, link previews, in-transaction side effects).
/// </summary>
public interface ISendMessageScope<TContext> where TContext : ScopeContext
{
    /// <summary>
    /// Authorizes the caller for the scope.
    /// Returns an error on failure, or a context on success.
    /// </summary>
    Task<AuthorizationResult<TContext>> AuthorizeAsync(UserId caller, CancellationToken ct);

    /// <summary>
    /// Applies scope-specific side effects that must participate in the same
    /// unit of work (e.g. unhiding participants on send).
    /// Called inside the transaction, before commit.
    /// </summary>
    Task ApplyInTransactionSideEffectsAsync(TContext context, CancellationToken ct);

    /// <summary>
    /// Notifies scope participants that a message was created.
    /// Implementation must use best-effort notification (fire-and-forget).
    /// </summary>
    Task NotifyMessageCreatedAsync(
        TContext context,
        Message message,
        IReadOnlyList<MessageAttachmentDto> attachments,
        ReplyPreviewDto? replyTo,
        CancellationToken ct);

    /// <summary>
    /// Validates that all mentioned user IDs exist and are members/participants of the scope.
    /// Returns a failure on the first invalid mention, or success.
    /// </summary>
    Task<Result> ValidateMentionedUsersAsync(
        IReadOnlyCollection<UserId> userIds,
        TContext context,
        CancellationToken ct);

    /// <summary>
    /// Triggers fire-and-forget link preview resolution for the given message.
    /// </summary>
    void ScheduleLinkPreviewResolution(
        TContext context,
        Message message,
        IReadOnlyList<Uri> urls,
        CancellationToken ct);
}

/// <summary>
/// Discriminated union for the result of scope authorization.
/// </summary>
public abstract record AuthorizationResult<TContext> where TContext : ScopeContext
{
    private AuthorizationResult() { }

    public sealed record Authorized(TContext Context) : AuthorizationResult<TContext>;

    public sealed record Denied(ApplicationError Error) : AuthorizationResult<TContext>;
}

/// <summary>
/// Result returned by <see cref="MessageSendOrchestrator"/> when a message
/// is successfully sent. The caller uses this to build the scope-specific response DTO.
/// </summary>
public sealed record MessageSendResult(
    Guid MessageId,
    Guid AuthorUserId,
    string? Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    ReplyPreviewDto? ReplyTo,
    IReadOnlyList<Guid> MentionedUserIds,
    DateTime CreatedAtUtc);
