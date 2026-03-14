using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.ReorderChannels;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class ReorderChannelsHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly ReorderChannelsHandler _handler;

    public ReorderChannelsHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _handler = new ReorderChannelsHandler(
            _guildRepositoryMock.Object,
            _guildChannelRepositoryMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<ReorderChannelsHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var request = new ReorderChannelsRequest([new ReorderChannelsItemRequest(GuildChannelId.New().ToString(), 0)]);
        var response = await _handler.HandleAsync(guildId, callerId, request);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, CallerRole: null));

        var request = new ReorderChannelsRequest([new ReorderChannelsItemRequest(GuildChannelId.New().ToString(), 0)]);
        var response = await _handler.HandleAsync(guild.Id, callerId, request);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdmin_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var request = new ReorderChannelsRequest([new ReorderChannelsItemRequest(GuildChannelId.New().ToString(), 0)]);
        var response = await _handler.HandleAsync(guild.Id, callerId, request);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelNotInGuild_ShouldReturnChannelNotFound()
    {
        var guild = CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var unknownChannelId = GuildChannelId.New().ToString();
        var request = new ReorderChannelsRequest([new ReorderChannelsItemRequest(unknownChannelId, 0)]);
        var response = await _handler.HandleAsync(guild.Id, adminId, request);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateChannelId_ShouldReturnValidationFailed()
    {
        var guild = CreateGuild();
        var adminId = UserId.New();
        var channel = CreateChannel(guild.Id, "ch1", 0);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([channel]);

        var channelIdStr = channel.Id.ToString();
        var request = new ReorderChannelsRequest([
            new ReorderChannelsItemRequest(channelIdStr, 0),
            new ReorderChannelsItemRequest(channelIdStr, 1)
        ]);
        var response = await _handler.HandleAsync(guild.Id, adminId, request);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminReordersChannels_ShouldReturnUpdatedPositions()
    {
        var guild = CreateGuild();
        var adminId = UserId.New();
        var ch1 = CreateChannel(guild.Id, "ch1", 0);
        var ch2 = CreateChannel(guild.Id, "ch2", 1);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ch1, ch2]);

        var request = new ReorderChannelsRequest([
            new ReorderChannelsItemRequest(ch1.Id.ToString(), 5),
            new ReorderChannelsItemRequest(ch2.Id.ToString(), 3)
        ]);
        var response = await _handler.HandleAsync(guild.Id, adminId, request);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.ToString());

        var reorderedCh1 = response.Data.Channels.First(c => c.ChannelId == ch1.Id.ToString());
        var reorderedCh2 = response.Data.Channels.First(c => c.ChannelId == ch2.Id.ToString());
        reorderedCh2.Position.Should().Be(3);
        reorderedCh1.Position.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminReordersChannels_ShouldPersistAndCommit()
    {
        var guild = CreateGuild();
        var adminId = UserId.New();
        var ch1 = CreateChannel(guild.Id, "ch1", 0);
        var ch2 = CreateChannel(guild.Id, "ch2", 1);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ch1, ch2]);

        var request = new ReorderChannelsRequest([
            new ReorderChannelsItemRequest(ch1.Id.ToString(), 5),
            new ReorderChannelsItemRequest(ch2.Id.ToString(), 3)
        ]);
        await _handler.HandleAsync(guild.Id, adminId, request);

        _guildChannelRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<GuildChannel>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPartialReorder_ShouldOnlyUpdateRequestedChannels()
    {
        var guild = CreateGuild();
        var adminId = UserId.New();
        var ch1 = CreateChannel(guild.Id, "ch1", 0);
        var ch2 = CreateChannel(guild.Id, "ch2", 1);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ch1, ch2]);

        var request = new ReorderChannelsRequest([
            new ReorderChannelsItemRequest(ch1.Id.ToString(), 10)
        ]);
        await _handler.HandleAsync(guild.Id, adminId, request);

        _guildChannelRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<GuildChannel>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Guild CreateGuild()
    {
        var nameResult = GuildName.Create("Test Guild");
        if (nameResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        var guildResult = Guild.Create(nameResult.Value!, UserId.New());
        if (guildResult.IsFailure)
            throw new InvalidOperationException("Failed to create guild for tests.");

        return guildResult.Value!;
    }

    private static GuildChannel CreateChannel(GuildId guildId, string name, int position)
    {
        var result = GuildChannel.Create(guildId, name, GuildChannelType.Text, isDefault: false, position);
        if (result.IsFailure)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return result.Value!;
    }
}
