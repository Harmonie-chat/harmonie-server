using FluentAssertions;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Services.Notifications;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Application.Tests.Notifications;

public sealed class MessageNotificationRecipientResolverTests
{
    [Fact]
    public void Resolve_ShouldExcludeAuthorFromCandidateRecipients()
    {
        var authorId = UserId.New();
        var recipientId = UserId.New();
        var context = new MessageNotificationContext(
            MessageId.New(),
            authorId,
            "alice",
            "Alice",
            new MessageNotificationTarget.Channel(GuildId.New(), "Harmonie", GuildChannelId.New(), "general"),
            new HashSet<UserId> { authorId, recipientId });
        var resolver = new MessageNotificationRecipientResolver();

        var recipients = resolver.Resolve(context);

        recipients.Should().ContainSingle().Which.Should().Be(recipientId);
    }
}
