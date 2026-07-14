using FluentAssertions;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Application.Tests.Notifications;

public sealed class MessageNotificationPolicyTests
{
    private readonly MessageNotificationPolicy _policy = new();

    [Fact]
    public void SelectRecipients_ForDirectConversation_ShouldNotifyParticipantsExceptAuthor()
    {
        var authorId = UserId.New();
        var recipientId = UserId.New();
        var context = CreateConversationContext(
            authorId,
            ConversationType.Direct,
            null,
            Set(authorId, recipientId));

        var recipients = _policy.SelectRecipients(context);

        recipients.Should().ContainSingle().Which.Should().Be(recipientId);
    }

    [Fact]
    public void SelectRecipients_ForGroupConversation_ShouldNotifyParticipantsExceptAuthor()
    {
        var authorId = UserId.New();
        var firstRecipientId = UserId.New();
        var secondRecipientId = UserId.New();
        var context = CreateConversationContext(
            authorId,
            ConversationType.Group,
            "Team",
            Set(authorId, firstRecipientId, secondRecipientId));

        var recipients = _policy.SelectRecipients(context);

        recipients.Should().BeEquivalentTo([firstRecipientId, secondRecipientId]);
    }

    [Fact]
    public void SelectRecipients_ForGuildChannel_ShouldNotifyChannelCandidatesExceptAuthor()
    {
        var authorId = UserId.New();
        var mentionedMemberId = UserId.New();
        var unmentionedMemberId = UserId.New();
        var context = CreateChannelContext(
            authorId,
            Set(authorId, mentionedMemberId, unmentionedMemberId),
            Set(mentionedMemberId));

        var recipients = _policy.SelectRecipients(context);

        recipients.Should().BeEquivalentTo([mentionedMemberId, unmentionedMemberId]);
    }

    [Fact]
    public void SelectRecipients_ForGuildChannel_ShouldExcludeAuthor()
    {
        var authorId = UserId.New();
        var recipientId = UserId.New();
        var context = CreateChannelContext(
            authorId,
            Set(authorId, recipientId),
            Set(authorId, recipientId));

        var recipients = _policy.SelectRecipients(context);

        recipients.Should().ContainSingle().Which.Should().Be(recipientId);
    }

    [Fact]
    public void SelectRecipients_ForGuildChannelWithOnlyAuthorCandidate_ShouldReturnNoRecipients()
    {
        var authorId = UserId.New();
        var context = CreateChannelContext(
            authorId,
            Set(authorId),
            Set());

        var recipients = _policy.SelectRecipients(context);

        recipients.Should().BeEmpty();
    }

    private static HashSet<UserId> Set(params UserId[] userIds)
        => userIds.ToHashSet();

    private static MessageNotificationContext CreateConversationContext(
        UserId authorId,
        ConversationType conversationType,
        string? conversationName,
        IReadOnlySet<UserId> participantIds)
    {
        return new MessageNotificationContext(
            MessageId.New(),
            authorId,
            "alice",
            "Alice",
            new MessageNotificationTarget.Conversation(ConversationId.New(), conversationType, conversationName),
            participantIds,
            new HashSet<UserId>());
    }

    private static MessageNotificationContext CreateChannelContext(
        UserId authorId,
        IReadOnlySet<UserId> guildMemberIds,
        IReadOnlySet<UserId> mentionedUserIds)
    {
        return new MessageNotificationContext(
            MessageId.New(),
            authorId,
            "alice",
            "Alice",
            new MessageNotificationTarget.Channel(GuildId.New(), "Harmonie", GuildChannelId.New(), "general"),
            guildMemberIds,
            mentionedUserIds);
    }
}
