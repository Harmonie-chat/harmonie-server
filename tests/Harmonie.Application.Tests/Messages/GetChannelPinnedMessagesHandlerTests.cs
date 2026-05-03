using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.GetPinnedMessages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class GetChannelPinnedMessagesHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IPinnedMessageRepository> _pinnedMessageRepositoryMock;
    private readonly GetPinnedMessagesHandler _handler;

    public GetChannelPinnedMessagesHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _pinnedMessageRepositoryMock = new Mock<IPinnedMessageRepository>();

        _handler = new GetPinnedMessagesHandler(
            _guildChannelRepositoryMock.Object,
            _pinnedMessageRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnChannelNotFound()
    {
        var channelId = GuildChannelId.New();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channelId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChannelAccessContext?)null);

        var response = await _handler.HandleAsync(new GetChannelPinnedMessagesInput(channelId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelIsVoice_ShouldReturnChannelNotText()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var response = await _handler.HandleAsync(new GetChannelPinnedMessagesInput(channel.Id), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnChannelAccessDenied()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, CallerRole: null));

        var response = await _handler.HandleAsync(new GetChannelPinnedMessagesInput(channel.Id), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenNoPinnedMessages_ShouldReturnEmptyList()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var emptyPage = new PinnedMessagesPage(Array.Empty<PinnedMessageSummary>(), null);
        _pinnedMessageRepositoryMock
            .Setup(x => x.GetPinnedMessagesAsync(channel.Id, callerId, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyPage);

        var response = await _handler.HandleAsync(new GetChannelPinnedMessagesInput(channel.Id), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenPinnedMessagesExist_ShouldReturnOrderedList()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var callerId = UserId.New();
        var now = DateTime.UtcNow;

        var summaries = new[]
        {
            new PinnedMessageSummary(
                MessageId: Guid.NewGuid(), AuthorUserId: Guid.NewGuid(),
                AuthorUsername: "second_user", AuthorDisplayName: "Second",
                Content: "second",
                Attachments: Array.Empty<MessageAttachmentDto>(),
                CreatedAtUtc: now.AddMinutes(-2), UpdatedAtUtc: null,
                PinnedByUserId: Guid.NewGuid(), PinnedAtUtc: now.AddMinutes(-1)),
            new PinnedMessageSummary(
                MessageId: Guid.NewGuid(), AuthorUserId: Guid.NewGuid(),
                AuthorUsername: "first_user", AuthorDisplayName: "First",
                Content: "first",
                Attachments: Array.Empty<MessageAttachmentDto>(),
                CreatedAtUtc: now.AddMinutes(-10), UpdatedAtUtc: null,
                PinnedByUserId: Guid.NewGuid(), PinnedAtUtc: now)
        };

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var page = new PinnedMessagesPage(summaries, null);
        _pinnedMessageRepositoryMock
            .Setup(x => x.GetPinnedMessagesAsync(channel.Id, callerId, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var response = await _handler.HandleAsync(new GetChannelPinnedMessagesInput(channel.Id), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.ChannelId.Should().Be(channel.Id.Value);
        response.Data.Items.Should().HaveCount(2);
        response.Data.Items[0].Content.Should().Be("second");
        response.Data.Items[1].Content.Should().Be("first");
    }
}
