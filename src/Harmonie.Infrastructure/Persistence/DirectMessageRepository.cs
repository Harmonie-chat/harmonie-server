using Dapper;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;

namespace Harmonie.Infrastructure.Persistence;

public sealed class DirectMessageRepository : IDirectMessageRepository
{
    private readonly DbSession _dbSession;

    public DirectMessageRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task AddAsync(
        DirectMessage message,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO direct_messages (
                               id,
                               conversation_id,
                               author_user_id,
                               content,
                               created_at_utc)
                           VALUES (
                               @Id,
                               @ConversationId,
                               @AuthorUserId,
                               @Content,
                               @CreatedAtUtc)
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = message.Id.Value,
                ConversationId = message.ConversationId.Value,
                AuthorUserId = message.AuthorUserId.Value,
                Content = message.Content.Value,
                message.CreatedAtUtc
            },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
