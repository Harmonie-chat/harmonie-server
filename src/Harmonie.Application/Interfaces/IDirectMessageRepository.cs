using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IDirectMessageRepository
{
    Task AddAsync(
        DirectMessage message,
        CancellationToken cancellationToken = default);

    Task<DirectMessagePage> GetMessagesAsync(
        ConversationId conversationId,
        DirectMessageCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<SearchConversationMessagesPage> SearchConversationMessagesAsync(
        SearchConversationMessagesQuery query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<DirectMessage?> GetByIdAsync(
        DirectMessageId messageId,
        CancellationToken cancellationToken = default);

    Task UpdateContentAsync(
        DirectMessage message,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(
        DirectMessage message,
        CancellationToken cancellationToken = default);
}

public sealed record DirectMessageCursor(
    DateTime CreatedAtUtc,
    DirectMessageId MessageId);

public sealed record DirectMessagePage(
    IReadOnlyList<DirectMessage> Items,
    DirectMessageCursor? NextCursor);

public sealed record SearchConversationMessagesQuery(
    ConversationId ConversationId,
    string SearchText,
    DateTime? BeforeCreatedAtUtc,
    DateTime? AfterCreatedAtUtc,
    DirectMessageCursor? Cursor);

public sealed record SearchConversationMessagesItem(
    DirectMessageId MessageId,
    UserId AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? AuthorAvatarUrl,
    MessageContent Content,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record SearchConversationMessagesPage(
    IReadOnlyList<SearchConversationMessagesItem> Items,
    DirectMessageCursor? NextCursor);
