using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class GetMessagesHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IMessagePaginationRepository> _channelMessageRepositoryMock;
    private readonly GetMessagesHandler _handler;

    public GetMessagesHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _channelMessageRepositoryMock = new Mock<IMessagePaginationRepository>();

        _handler = new GetMessagesHandler(
            _guildChannelRepositoryMock.Object,
            _channelMessageRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenCursorIsInvalid_ShouldReturnValidationFailure()
    {
        var response = await _handler.HandleAsync(
            new GetChannelMessagesInput(GuildChannelId.New(), Before: "invalid-cursor", Limit: 50),
            UserId.New(),
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelIsVoice_ShouldReturnNotText()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var response = await _handler.HandleAsync(
            new GetChannelMessagesInput(channel.Id, Limit: 50),
            userId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotMember_ShouldReturnAccessDenied()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, CallerRole: null));

        var response = await _handler.HandleAsync(
            new GetChannelMessagesInput(channel.Id, Limit: 50),
            userId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldReturnMessagesAscending()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var first = ApplicationTestBuilders.CreateChannelMessage(channel.Id, userId, content: "First", createdAtUtc: DateTime.UtcNow.AddMinutes(-2));
        var second = ApplicationTestBuilders.CreateChannelMessage(channel.Id, userId, content: "Second", createdAtUtc: DateTime.UtcNow.AddMinutes(-1));
        var nextCursor = new MessageCursor(first.CreatedAtUtc, first.Id);

        _channelMessageRepositoryMock
            .Setup(x => x.GetChannelPageAsync(
                channel.Id,
                It.IsAny<MessageCursor?>(),
                50,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagePage(
                [second, first],
                nextCursor,
                new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>()));

        var response = await _handler.HandleAsync(
            new GetChannelMessagesInput(channel.Id, Limit: 50),
            userId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(2);
        response.Data.Items[0].Content.Should().Be("First");
        response.Data.Items[0].Attachments.Should().BeEmpty();
        response.Data.Items[0].Reactions.Should().BeEmpty();
        response.Data.Items[0].LinkPreviews.Should().BeNull();
        response.Data.Items[1].Content.Should().Be("Second");
        response.Data.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenMessagesHaveLinkPreviews_ShouldIncludeThem()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, userId, content: "Check https://example.com", createdAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var previews = new Dictionary<Guid, IReadOnlyList<LinkPreviewDto>>
        {
            [message.Id.Value] = [new LinkPreviewDto("https://example.com", "Example", "Description", null, "Example Site")]
        };

        _channelMessageRepositoryMock
            .Setup(x => x.GetChannelPageAsync(
                channel.Id,
                It.IsAny<MessageCursor?>(),
                50,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagePage(
                [message],
                null,
                new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>(),
                previews));

        var response = await _handler.HandleAsync(
            new GetChannelMessagesInput(channel.Id, Limit: 50),
            userId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(1);
        response.Data.Items[0].LinkPreviews.Should().NotBeNull();
        response.Data.Items[0].LinkPreviews.Should().HaveCount(1);
        var firstPreview = response.Data.Items[0].LinkPreviews![0];
        firstPreview.Url.Should().Be("https://example.com");
        firstPreview.Title.Should().Be("Example");
        firstPreview.Description.Should().Be("Description");
        firstPreview.SiteName.Should().Be("Example Site");
    }

    [Fact]
    public async Task HandleAsync_WhenMessageHasNoReply_ShouldHaveNullReplyTo()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, userId, content: "no reply", createdAtUtc: DateTime.UtcNow.AddMinutes(-1));

        _channelMessageRepositoryMock
            .Setup(x => x.GetChannelPageAsync(
                channel.Id,
                It.IsAny<MessageCursor?>(),
                50,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagePage(
                [message],
                null,
                new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>()));

        var response = await _handler.HandleAsync(
            new GetChannelMessagesInput(channel.Id, Limit: 50),
            userId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(1);
        response.Data.Items[0].ReplyTo.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenMessageHasReplyTo_ShouldIncludeReplyToFromPageData()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();
        var targetMessageId = MessageId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var message = ApplicationTestBuilders.CreateChannelMessage(
            channel.Id, userId, content: "replying",
            createdAtUtc: DateTime.UtcNow.AddMinutes(-1),
            replyToMessageId: targetMessageId);

        var replyPreviews = new Dictionary<Guid, ReplyPreviewDto>
        {
            [targetMessageId.Value] = new ReplyPreviewDto(
                targetMessageId.Value,
                Guid.NewGuid(),
                "Target Display",
                "targetuser",
                "target message content",
                true,
                false,
                null)
        };

        _channelMessageRepositoryMock
            .Setup(x => x.GetChannelPageAsync(
                channel.Id,
                It.IsAny<MessageCursor?>(),
                50,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagePage(
                [message],
                null,
                new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>(),
                ReplyPreviewsByTargetMessageId: replyPreviews));

        var response = await _handler.HandleAsync(
            new GetChannelMessagesInput(channel.Id, Limit: 50),
            userId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(1);
        var replyTo = response.Data.Items[0].ReplyTo!;
        replyTo.MessageId.Should().Be(targetMessageId.Value);
        replyTo.AuthorUsername.Should().Be("targetuser");
        replyTo.AuthorDisplayName.Should().Be("Target Display");
        replyTo.Content.Should().Be("target message content");
        replyTo.HasAttachments.Should().BeTrue();
    }

}
