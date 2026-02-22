namespace Harmonie.Application.Features.Channels.GetMessages;

public sealed record GetMessagesResponse(
    string ChannelId,
    IReadOnlyList<GetMessagesItemResponse> Items,
    string? NextCursor);

public sealed record GetMessagesItemResponse(
    string MessageId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);
