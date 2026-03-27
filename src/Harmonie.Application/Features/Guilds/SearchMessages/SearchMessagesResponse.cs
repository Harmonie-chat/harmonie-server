using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Guilds.SearchMessages;

public sealed record SearchMessagesResponse(
    Guid GuildId,
    IReadOnlyList<SearchMessagesItemResponse> Items,
    string? NextCursor);

public sealed record SearchMessagesItemResponse(
    Guid MessageId,
    Guid ChannelId,
    string ChannelName,
    Guid AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    Guid? AuthorAvatarFileId,
    AvatarAppearanceDto? AuthorAvatar,
    string Content,
    IReadOnlyList<MessageAttachmentDto> Attachments,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
