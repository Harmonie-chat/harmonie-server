using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Tests.Common;

internal static class ApplicationTestBuilders
{
    public static User CreateUser(UserId? userId = null)
    {
        var emailResult = Email.Create($"test-{Guid.NewGuid():N}@harmonie.chat");
        if (emailResult.IsFailure || emailResult.Value is null)
            throw new InvalidOperationException("Failed to create email for tests.");

        var usernameResult = Username.Create($"user{Guid.NewGuid():N}"[..20]);
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Failed to create username for tests.");

        if (userId is null)
        {
            var userResult = User.Create(emailResult.Value, usernameResult.Value, "hashed_password");
            if (userResult.IsFailure || userResult.Value is null)
                throw new InvalidOperationException("Failed to create user for tests.");
            return userResult.Value;
        }

        return User.Rehydrate(
            userId,
            emailResult.Value,
            usernameResult.Value,
            passwordHash: "hashed_password",
            avatarFileId: null,
            isEmailVerified: true,
            isActive: true,
            lastLoginAtUtc: null,
            displayName: null,
            bio: null,
            avatarColor: null,
            avatarIcon: null,
            avatarBg: null,
            theme: "default",
            language: null,
            status: "online",
            statusUpdatedAtUtc: null,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: DateTime.UtcNow);
    }

    public static Guild CreateGuild(UserId? ownerId = null, GuildId? guildId = null, UploadedFileId? iconFileId = null)
    {
        var nameResult = GuildName.Create($"guild-{Guid.NewGuid():N}"[..20]);
        if (nameResult.IsFailure || nameResult.Value is null)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        return Guild.Rehydrate(
            guildId ?? GuildId.New(),
            nameResult.Value,
            ownerId ?? UserId.New(),
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: DateTime.UtcNow,
            iconFileId: iconFileId);
    }

    public static GuildChannel CreateChannel(
        GuildChannelType type = GuildChannelType.Text,
        GuildId? guildId = null,
        string name = "general")
    {
        var result = GuildChannel.Create(
            guildId ?? GuildId.New(),
            name,
            type,
            isDefault: false,
            position: 0);

        if (result.IsFailure || result.Value is null)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return result.Value;
    }

    public static Message CreateChannelMessage(
        GuildChannelId channelId,
        UserId authorId,
        string content = "test content",
        DateTime? createdAtUtc = null,
        IReadOnlyList<MessageAttachment>? attachments = null)
    {
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Failed to create message content for tests.");

        return Message.Rehydrate(
            id: MessageId.New(),
            channelId: channelId,
            conversationId: null,
            authorUserId: authorId,
            content: contentResult.Value,
            createdAtUtc: createdAtUtc ?? DateTime.UtcNow,
            updatedAtUtc: null,
            deletedAtUtc: null,
            attachments: attachments);
    }

    public static Message CreateConversationMessage(
        ConversationId conversationId,
        UserId authorUserId,
        string content = "original content",
        DateTime? createdAtUtc = null,
        IReadOnlyList<MessageAttachment>? attachments = null)
    {
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Failed to create conversation message content for tests.");

        return Message.Rehydrate(
            id: MessageId.New(),
            channelId: null,
            conversationId: conversationId,
            authorUserId: authorUserId,
            content: contentResult.Value,
            createdAtUtc: createdAtUtc ?? DateTime.UtcNow.AddMinutes(-1),
            updatedAtUtc: null,
            deletedAtUtc: null,
            attachments: attachments);
    }

    public static Conversation CreateConversation(UserId user1Id, UserId user2Id)
        => Conversation.Rehydrate(
            ConversationId.New(),
            Harmonie.Domain.Entities.Conversations.ConversationType.Direct,
            null,
            DateTime.UtcNow);

    public static Conversation CreateGroupConversation(string? name, params UserId[] participantIds)
        => Conversation.Rehydrate(
            ConversationId.New(),
            Harmonie.Domain.Entities.Conversations.ConversationType.Group,
            name,
            DateTime.UtcNow);

    public static UploadedFile CreateUploadedFile(
        UserId? uploaderUserId = null,
        UploadedFileId? id = null,
        UploadPurpose purpose = UploadPurpose.Attachment,
        string? storageKey = null,
        string fileName = "file.bin",
        string contentType = "application/octet-stream",
        long sizeBytes = 123)
    {
        return UploadedFile.Rehydrate(
            id ?? UploadedFileId.New(),
            uploaderUserId ?? UserId.New(),
            fileName,
            contentType,
            sizeBytes,
            storageKey ?? $"uploads/{Guid.NewGuid():N}.bin",
            purpose,
            DateTime.UtcNow.AddMinutes(-10));
    }
}
