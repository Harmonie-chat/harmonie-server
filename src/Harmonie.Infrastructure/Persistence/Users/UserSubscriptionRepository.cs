using Dapper;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Harmonie.Infrastructure.Persistence.Common;

namespace Harmonie.Infrastructure.Persistence.Users;

public sealed class UserSubscriptionRepository : IUserSubscriptionRepository
{
    private readonly DbSession _dbSession;

    public UserSubscriptionRepository(DbSession dbSession)
    {
        _dbSession = dbSession;
    }

    public async Task<UserSubscriptions> GetAllAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT g.id
                           FROM guild_members gm
                           JOIN guilds g ON g.id = gm.guild_id
                           WHERE gm.user_id = @UserId;

                           SELECT gc.id
                           FROM guild_members gm
                           JOIN guild_channels gc ON gc.guild_id = gm.guild_id AND gc.type = 1
                           WHERE gm.user_id = @UserId;

                           SELECT cp.conversation_id
                           FROM conversation_participants cp
                           WHERE cp.user_id = @UserId;
                           """;

        var connection = await _dbSession.GetOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { UserId = userId.Value },
            transaction: _dbSession.Transaction,
            cancellationToken: cancellationToken);

        using var multi = await connection.QueryMultipleAsync(command);
        var guildIds = (await multi.ReadAsync<Guid>()).Select(GuildId.From).ToArray();
        var channelIds = (await multi.ReadAsync<Guid>()).Select(GuildChannelId.From).ToArray();
        var conversationIds = (await multi.ReadAsync<Guid>()).Select(ConversationId.From).ToArray();

        return new UserSubscriptions(guildIds, channelIds, conversationIds);
    }
}
