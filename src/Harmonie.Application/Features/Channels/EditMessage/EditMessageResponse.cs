namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed record EditMessageResponse(
    string MessageId,
    string ChannelId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc);
