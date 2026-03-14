using Harmonie.Application.Common;
using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Conversations.SearchConversationMessages;

public sealed record SearchConversationMessagesResponse(
    string ConversationId,
    IReadOnlyList<SearchConversationMessagesItemResponse> Items,
    string? NextCursor);

public sealed record SearchConversationMessagesItemResponse(
    string MessageId,
    string AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string? AuthorAvatarFileId,
    AvatarAppearanceDto? AuthorAvatar,
    string Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
