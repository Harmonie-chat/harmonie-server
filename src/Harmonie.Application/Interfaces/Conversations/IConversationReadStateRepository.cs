using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Conversations;

public interface IConversationReadStateRepository
{
    Task UpsertAsync(
        ConversationReadState state,
        CancellationToken cancellationToken = default);

    Task<ConversationReadState?> GetAsync(
        UserId userId,
        ConversationId conversationId,
        CancellationToken cancellationToken = default);
}
