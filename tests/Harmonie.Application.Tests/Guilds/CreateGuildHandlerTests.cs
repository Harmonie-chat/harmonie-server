using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Guilds;

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

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _guildMemberRepositoryMock
            .Setup(x => x.TryAddAsync(It.IsAny<GuildMember>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _handler = new CreateGuildHandler(
            _guildRepositoryMock.Object,
            _guildMemberRepositoryMock.Object,
            _guildChannelRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
            _unitOfWorkMock.Object,
            NullLogger<CreateGuildHandler>.Instance);
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
        response.Data.IconFileId.Should().BeNull();
        response.Data.Icon.Should().BeNull();

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

    [Fact]
    public async Task HandleAsync_WithIconFields_ShouldReturnIconInResponse()
    {
        var iconFileId = UploadedFileId.New();
        var request = new CreateGuildRequest(
            "Icon Guild",
            IconFileId: iconFileId.ToString(),
            Icon: new CreateGuildIconRequest(
                Color: "#7C3AED",
                Name: "sword",
                Bg: "#1F2937"));
        var userId = UserId.New();

        var response = await _handler.HandleAsync(request, userId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.IconFileId.Should().Be(iconFileId.ToString());
        response.Data.Icon.Should().NotBeNull();
        response.Data.Icon!.Color.Should().Be("#7C3AED");
        response.Data.Icon.Name.Should().Be("sword");
        response.Data.Icon.Bg.Should().Be("#1F2937");
    }

    [Fact]
    public async Task HandleAsync_WithPartialIconFields_ShouldReturnPartialIcon()
    {
        var request = new CreateGuildRequest(
            "Partial Icon Guild",
            Icon: new CreateGuildIconRequest(Color: "#F59E0B"));
        var userId = UserId.New();

        var response = await _handler.HandleAsync(request, userId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.IconFileId.Should().BeNull();
        response.Data.Icon.Should().NotBeNull();
        response.Data.Icon!.Color.Should().Be("#F59E0B");
        response.Data.Icon.Name.Should().BeNull();
        response.Data.Icon.Bg.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WithOnlyIconFileId_ShouldReturnIconFileIdWithoutIcon()
    {
        var iconFileId = UploadedFileId.New();
        var request = new CreateGuildRequest(
            "Url Only Guild",
            IconFileId: iconFileId.ToString());
        var userId = UserId.New();

        var response = await _handler.HandleAsync(request, userId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.IconFileId.Should().Be(iconFileId.ToString());
        response.Data.Icon.Should().BeNull();
    }
}
