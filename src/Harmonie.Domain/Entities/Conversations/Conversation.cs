using Harmonie.Domain.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Domain.Entities.Conversations;

public sealed class Conversation : Entity<ConversationId>
{
    public ConversationType Type { get; private set; }

    public string? Name { get; private set; }

    private Conversation(
        ConversationId id,
        ConversationType type,
        string? name,
        DateTime createdAtUtc)
    {
        Id = id;
        Type = type;
        Name = name;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = null;
    }

    public static Result<Conversation> CreateDirect(UserId firstUserId, UserId secondUserId)
    {
        if (firstUserId is null)
            return Result.Failure<Conversation>("First user ID is required");

        if (secondUserId is null)
            return Result.Failure<Conversation>("Second user ID is required");

        if (firstUserId == secondUserId)
            return Result.Failure<Conversation>("Conversation participants must be different users");

        return Result.Success(new Conversation(
            ConversationId.New(),
            ConversationType.Direct,
            null,
            DateTime.UtcNow));
    }

    public static Result<Conversation> CreateGroup(string? name, IReadOnlyList<UserId> participantIds)
    {
        if (participantIds is null || participantIds.Count < 2)
            return Result.Failure<Conversation>("A group conversation requires at least 2 participants");

        return Result.Success(new Conversation(
            ConversationId.New(),
            ConversationType.Group,
            name,
            DateTime.UtcNow));
    }

    public static Conversation Rehydrate(
        ConversationId id,
        ConversationType type,
        string? name,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new Conversation(id, type, name, createdAtUtc);
    }

    public Result UpdateName(string? name)
    {
        if (name is not null && string.IsNullOrWhiteSpace(name))
            return Result.Failure("Conversation name cannot be empty");

        if (name is not null && name.Length > 100)
            return Result.Failure("Conversation name must be 100 characters or less");

        Name = name;
        return Result.Success();
    }
}
