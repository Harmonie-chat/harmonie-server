using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Conversations.SearchConversationMessages;

public sealed record SearchConversationMessagesResponse(
    Guid ConversationId,
    IReadOnlyList<SearchConversationMessagesItemResponse> Items,
    string? NextCursor);

public sealed record SearchConversationMessagesItemResponse(
    Guid MessageId,
    Guid AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    Guid? AuthorAvatarFileId,
    AvatarAppearanceDto? AuthorAvatar,
    string Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
