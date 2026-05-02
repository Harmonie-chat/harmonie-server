using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.GetReactionUsers;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class GetChannelReactionUsersHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IMessageReactionRepository> _reactionRepositoryMock;
    private readonly GetReactionUsersHandler _handler;

    public GetChannelReactionUsersHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _reactionRepositoryMock = new Mock<IMessageReactionRepository>();

        _handler = new GetReactionUsersHandler(
            _guildChannelRepositoryMock.Object,
            _messageRepositoryMock.Object,
            _reactionRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenNoReactions_ShouldReturnEmptyUsers()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, callerId, content: "test");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _reactionRepositoryMock
            .Setup(x => x.GetReactionUsersAsync(messageId, "👍", It.IsAny<int>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReactionUsersPage(Array.Empty<ReactionUser>(), 0, null));

        var response = await _handler.HandleAsync(
            new GetChannelReactionUsersInput(channel.Id, messageId, "👍"),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Users.Should().BeEmpty();
        response.Data.TotalCount.Should().Be(0);
        response.Data.Emoji.Should().Be("👍");
    }

    [Fact]
    public async Task HandleAsync_WithReactions_ShouldReturnUsers()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channel.Id, callerId, content: "test");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _reactionRepositoryMock
            .Setup(x => x.GetReactionUsersAsync(messageId, "👍", It.IsAny<int>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReactionUsersPage(
                new[] { new ReactionUser(callerId.Value, "testuser", "Test User") },
                1,
                null));

        var response = await _handler.HandleAsync(
            new GetChannelReactionUsersInput(channel.Id, messageId, "👍"),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Users.Should().HaveCount(1);
        response.Data.TotalCount.Should().Be(1);
        response.Data.Users[0].UserId.Should().Be(callerId.Value);
        response.Data.Users[0].Username.Should().Be("testuser");
        response.Data.Users[0].DisplayName.Should().Be("Test User");
    }
}
