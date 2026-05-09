using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Common.Messages;

/// <summary>
/// Result returned by <see cref="MessageFetchOrchestrator"/> when messages are
/// successfully fetched. The caller maps this to the scope-specific response DTO.
/// </summary>
public sealed record MessageFetchResult(
    IReadOnlyList<GetMessagesItemResponse> Items,
    string? NextCursor,
    Guid? LastReadMessageId,
    DateTime? LastReadAtUtc);

/// <summary>
/// Shared orchestrator for GetMessages across all scopes.
/// Extracts the duplicated cursor parsing, item mapping, and response assembly.
/// </summary>
public sealed class MessageFetchOrchestrator
{
    private const int DefaultLimit = 50;

    public async Task<ApplicationResponse<MessageFetchResult>> FetchAsync<TContext>(
        IMessagePageScope<TContext> scope,
        string? rawCursor,
        int? rawLimit,
        UserId callerId,
        CancellationToken ct)
        where TContext : ScopeContext
    {
        // ── Cursor parsing ──────────────────────────────────────────────
        MessageCursor? cursor = null;
        if (rawCursor is not null)
        {
            if (!MessageCursorCodec.TryParse(rawCursor, out var parsedCursor) || parsedCursor is null)
            {
                return ApplicationResponse<MessageFetchResult>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        "cursor",
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Cursor is invalid"));
            }

            cursor = parsedCursor;
        }

        var limit = Math.Clamp(rawLimit ?? DefaultLimit, 1, 100);

        // ── Authorization ───────────────────────────────────────────────
        var authResult = await scope.AuthorizeAsync(callerId, ct);
        if (authResult is AuthorizationResult<TContext>.Denied denied)
            return ApplicationResponse<MessageFetchResult>.Fail(denied.Error);

        // ── Fetch page ──────────────────────────────────────────────────
        var page = await scope.GetPageAsync(cursor, limit, callerId, ct);

        // ── Map items ───────────────────────────────────────────────────
        var items = page.Items
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id.Value)
            .Select(x =>
            {
                page.ReactionsByMessageId.TryGetValue(x.Id.Value, out var reactions);
                page.AttachmentsByMessageId.TryGetValue(x.Id.Value, out var attachments);
                IReadOnlyList<LinkPreviewDto>? previews = null;
                page.LinkPreviewsByMessageId?.TryGetValue(x.Id.Value, out previews);
                var isPinned = page.PinnedMessageIds?.Contains(x.Id.Value) == true;
                ReplyPreviewDto? replyTo = null;
                if (x.ReplyToMessageId is not null)
                {
                    page.ReplyPreviewsByTargetMessageId?.TryGetValue(x.ReplyToMessageId.Value, out replyTo);
                }
                IReadOnlyList<Guid>? mentionedIds = null;
                page.MentionedUserIdsByMessageId?.TryGetValue(x.Id.Value, out mentionedIds);
                var mentionedUserIds = mentionedIds ?? Array.Empty<Guid>();
                return new GetMessagesItemResponse(
                    MessageId: x.Id.Value,
                    AuthorUserId: x.AuthorUserId.Value,
                    Content: x.Content?.Value,
                    Attachments: attachments?.Select(MessageAttachmentDto.FromDomain).ToArray()
                                 ?? Array.Empty<MessageAttachmentDto>(),
                    Reactions: reactions?.Select(r => new MessageReactionDto(r.Emoji, r.Count, r.ReactedByCaller,
                        r.Users.Select(u => new ReactionUserDto(u.UserId, u.Username, u.DisplayName)).ToArray())).ToArray()
                              ?? Array.Empty<MessageReactionDto>(),
                    LinkPreviews: previews?.ToArray(),
                    IsPinned: isPinned,
                    ReplyTo: replyTo,
                    MentionedUserIds: mentionedUserIds?.ToArray() ?? Array.Empty<Guid>(),
                    CreatedAtUtc: x.CreatedAtUtc,
                    UpdatedAtUtc: x.UpdatedAtUtc);
            })
            .ToArray();

        // ── Build result ────────────────────────────────────────────────
        return ApplicationResponse<MessageFetchResult>.Ok(new MessageFetchResult(
            Items: items,
            NextCursor: page.NextCursor is null ? null : MessageCursorCodec.Encode(page.NextCursor),
            LastReadMessageId: page.LastReadState?.LastReadMessageId.Value,
            LastReadAtUtc: page.LastReadState?.ReadAtUtc));
    }
}
