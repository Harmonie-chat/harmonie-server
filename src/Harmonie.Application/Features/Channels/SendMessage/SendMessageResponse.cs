namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed record SendMessageResponse(
    string MessageId,
    string ChannelId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);
