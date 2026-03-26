namespace Harmonie.Application.Features.Conversations.CreateGroupConversation;

public sealed record CreateGroupConversationRequest(string? Name, IReadOnlyList<Guid> ParticipantUserIds);
