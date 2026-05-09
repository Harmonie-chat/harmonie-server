namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed record EditMessageRequest(string Content, IReadOnlyList<Guid>? MentionedUserIds = null);
