using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class GetMessagesHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IMessageRepository> _channelMessageRepositoryMock;
    private readonly GetMessagesHandler _handler;

    public GetMessagesHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _channelMessageRepositoryMock = new Mock<IMessageRepository>();

        _handler = new GetMessagesHandler(
            _guildChannelRepositoryMock.Object,
            _channelMessageRepositoryMock.Object,
            NullLogger<GetMessagesHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenCursorIsInvalid_ShouldReturnValidationFailure()
    {
        var response = await _handler.HandleAsync(
            GuildChannelId.New(),
            new GetMessagesRequest { Before = "invalid-cursor", Limit = 50 },
            UserId.New());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelIsVoice_ShouldReturnNotText()
    {
        var channel = CreateChannel(GuildChannelType.Voice);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var response = await _handler.HandleAsync(
            channel.Id,
            new GetMessagesRequest { Limit = 50 },
            userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotMember_ShouldReturnAccessDenied()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, CallerRole: null));

        var response = await _handler.HandleAsync(
            channel.Id,
            new GetMessagesRequest { Limit = 50 },
            userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldReturnMessagesAscending()
    {
        var channel = CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var first = CreateMessage(channel.Id, userId, "First", DateTime.UtcNow.AddMinutes(-2));
        var second = CreateMessage(channel.Id, userId, "Second", DateTime.UtcNow.AddMinutes(-1));
        var nextCursor = new MessageCursor(first.CreatedAtUtc, first.Id);

        _channelMessageRepositoryMock
            .Setup(x => x.GetChannelPageAsync(
                channel.Id,
                It.IsAny<MessageCursor?>(),
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagePage([second, first], nextCursor));

        var response = await _handler.HandleAsync(
            channel.Id,
            new GetMessagesRequest { Limit = 50 },
            userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(2);
        response.Data.Items[0].Content.Should().Be("First");
        response.Data.Items[1].Content.Should().Be("Second");
        response.Data.NextCursor.Should().NotBeNullOrEmpty();
    }

    private static GuildChannel CreateChannel(GuildChannelType type)
    {
        var channelResult = GuildChannel.Create(
            GuildId.New(),
            "general",
            type,
            isDefault: true,
            position: 0);
        if (channelResult.IsFailure)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return channelResult.Value!;
    }

    private static Message CreateMessage(
        GuildChannelId channelId,
        UserId authorUserId,
        string content,
        DateTime createdAtUtc)
    {
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsFailure)
            throw new InvalidOperationException("Failed to create message content for tests.");

        return Message.Rehydrate(
            id: MessageId.New(),
            channelId: channelId,
            conversationId: null,
            authorUserId: authorUserId,
            content: contentResult.Value!,
            createdAtUtc: createdAtUtc,
            updatedAtUtc: null,
            deletedAtUtc: null);
    }
}
