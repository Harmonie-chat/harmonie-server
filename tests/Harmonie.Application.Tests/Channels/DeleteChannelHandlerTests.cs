using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.DeleteChannel;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Channels;

public sealed class DeleteChannelHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IGuildNotifier> _guildNotifierMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly DeleteChannelHandler _handler;

    public DeleteChannelHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _guildNotifierMock = new Mock<IGuildNotifier>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _guildNotifierMock
            .Setup(x => x.NotifyChannelDeletedAsync(
                It.IsAny<ChannelDeletedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new DeleteChannelHandler(
            _guildChannelRepositoryMock.Object,
            _guildNotifierMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<DeleteChannelHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var channelId = GuildChannelId.New();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channelId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChannelAccessContext?)null);

        var response = await _handler.HandleAsync(channelId, callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnChannelAccessDenied()
    {
        var channel = CreateChannel();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, CallerRole: null));

        var response = await _handler.HandleAsync(channel.Id, callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdmin_ShouldReturnGuildAccessDenied()
    {
        var channel = CreateChannel();
        var callerId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var response = await _handler.HandleAsync(channel.Id, callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelIsDefault_ShouldReturnCannotDeleteDefault()
    {
        var channel = CreateChannel(isDefault: true);
        var adminId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        var response = await _handler.HandleAsync(channel.Id, adminId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.CannotDeleteDefault);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminDeletesChannel_ShouldSucceedAndCallDeleteAndCommit()
    {
        var channel = CreateChannel(isDefault: false);
        var adminId = UserId.New();

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.DeleteAsync(channel.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(channel.Id, adminId, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().BeTrue();

        _guildChannelRepositoryMock.Verify(
            x => x.DeleteAsync(channel.Id, It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _guildNotifierMock.Verify(
            x => x.NotifyChannelDeletedAsync(
                It.Is<ChannelDeletedNotification>(n =>
                    n.ChannelId == channel.Id &&
                    n.GuildId == channel.GuildId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static GuildChannel CreateChannel(string name = "general", bool isDefault = false)
    {
        var result = GuildChannel.Create(
            GuildId.New(),
            name,
            GuildChannelType.Text,
            isDefault: isDefault,
            position: 0,
            createdAtUtc: TestTime.UtcNow);

        if (result.IsFailure)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return result.Value!;
    }
}
