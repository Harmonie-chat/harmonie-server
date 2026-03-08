namespace Harmonie.Application.Features.Guilds.SearchMessages;

public sealed record SearchMessagesResponse(
    string GuildId,
    IReadOnlyList<SearchMessagesItemResponse> Items,
    string? NextCursor);

public sealed record SearchMessagesItemResponse(
    string MessageId,
    string ChannelId,
    string ChannelName,
    string AuthorUserId,
    string AuthorUsername,
    string? AuthorDisplayName,
    string Content,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
