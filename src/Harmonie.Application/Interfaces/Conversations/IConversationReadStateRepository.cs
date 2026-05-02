using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Interfaces.Conversations;

public interface IConversationReadStateRepository
{
    Task UpsertAsync(
        MessageReadState state,
        CancellationToken cancellationToken = default);

    Task<MessageReadState?> GetAsync(
        UserId userId,
        ConversationId conversationId,
        CancellationToken cancellationToken = default);
}
