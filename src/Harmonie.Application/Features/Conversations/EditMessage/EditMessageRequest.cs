namespace Harmonie.Application.Features.Conversations.EditMessage;

public sealed record EditMessageRequest(string Content, IReadOnlyList<Guid>? MentionedUserIds = null);
