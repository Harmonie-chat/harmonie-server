using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Interfaces;

public interface IMessageSearchRepository
{
    Task<SearchGuildMessagesPage> SearchGuildMessagesAsync(
        SearchGuildMessagesQuery query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<SearchConversationMessagesPage> SearchConversationMessagesAsync(
        SearchConversationMessagesQuery query,
        int limit,
        CancellationToken cancellationToken = default);
}
