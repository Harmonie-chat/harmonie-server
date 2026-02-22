using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class CreateGuildHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildMemberRepository> _guildMemberRepositoryMock;
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly CreateGuildHandler _handler;

    public CreateGuildHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildMemberRepositoryMock = new Mock<IGuildMemberRepository>();
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _guildMemberRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new CreateGuildHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _guildChannelRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldCreateGuildAndDefaultChannels()
    {
        var request = new CreateGuildRequest("Harmonie Team");
        var userId = UserId.New();

        var response = await _handler.HandleAsync(request, userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().Be("Harmonie Team");
        response.Data.OwnerUserId.Should().Be(userId.ToString());

        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _guildRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()), Times.Once);
        _guildMemberRepositoryMock.Verify(
            x => x.TryAddAsync(
                It.Is<GuildMember>(member => member.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _guildChannelRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<GuildChannel>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidName_ShouldReturnValidationFailure()
    {
        var request = new CreateGuildRequest("ab");
        var userId = UserId.New();

        var response = await _handler.HandleAsync(request, userId);

        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
