using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.ReorderChannels;
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

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new ReorderChannelsHandler(
            _guildRepositoryMock.Object,
            _guildChannelRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var request = new ReorderChannelsRequest([new ReorderChannelsItemRequest(GuildChannelId.New(), 0)]);
        var response = await _handler.HandleAsync(new ReorderChannelsInput(guildId, request.Channels), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotMember_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, CallerRole: null));

        var request = new ReorderChannelsRequest([new ReorderChannelsItemRequest(GuildChannelId.New(), 0)]);
        var response = await _handler.HandleAsync(new ReorderChannelsInput(guild.Id, request.Channels), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdmin_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var request = new ReorderChannelsRequest([new ReorderChannelsItemRequest(GuildChannelId.New(), 0)]);
        var response = await _handler.HandleAsync(new ReorderChannelsInput(guild.Id, request.Channels), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelNotInGuild_ShouldReturnChannelNotFound()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var unknownChannelId = GuildChannelId.New();
        var request = new ReorderChannelsRequest([new ReorderChannelsItemRequest(unknownChannelId, 0)]);
        var response = await _handler.HandleAsync(new ReorderChannelsInput(guild.Id, request.Channels), adminId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateChannelId_ShouldReturnValidationFailed()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();
        var channel = CreateChannel(guild.Id, "ch1", 0);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.GetByGuildIdAsync(guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([channel]);

        var channelId = channel.Id;
        var request = new ReorderChannelsRequest([
            new ReorderChannelsItemRequest(channelId, 0),
            new ReorderChannelsItemRequest(channelId, 1)
        ]);
        var response = await _handler.HandleAsync(new ReorderChannelsInput(guild.Id, request.Channels), adminId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminReordersChannels_ShouldReturnUpdatedPositions()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
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
            new ReorderChannelsItemRequest(ch1.Id, 5),
            new ReorderChannelsItemRequest(ch2.Id, 3)
        ]);
        var response = await _handler.HandleAsync(new ReorderChannelsInput(guild.Id, request.Channels), adminId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.Value);

        var reorderedCh1 = response.Data.Channels.First(c => c.ChannelId == ch1.Id.Value);
        var reorderedCh2 = response.Data.Channels.First(c => c.ChannelId == ch2.Id.Value);
        reorderedCh2.Position.Should().Be(3);
        reorderedCh1.Position.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminReordersChannels_ShouldPersistAndCommit()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
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
            new ReorderChannelsItemRequest(ch1.Id, 5),
            new ReorderChannelsItemRequest(ch2.Id, 3)
        ]);
        await _handler.HandleAsync(new ReorderChannelsInput(guild.Id, request.Channels), adminId);

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
        var guild = ApplicationTestBuilders.CreateGuild();
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
            new ReorderChannelsItemRequest(ch1.Id, 10)
        ]);
        await _handler.HandleAsync(new ReorderChannelsInput(guild.Id, request.Channels), adminId);

        _guildChannelRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<GuildChannel>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static GuildChannel CreateChannel(GuildId guildId, string name, int position)
    {
        var result = GuildChannel.Create(guildId, name, GuildChannelType.Text, isDefault: false, position);
        if (result.IsFailure)
            throw new InvalidOperationException("Failed to create channel for tests.");

        return result.Value!;
    }
}
