using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Guilds.DeleteGuild;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Guilds;

public sealed class DeleteGuildHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildNotifier> _guildNotifierMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly DeleteGuildHandler _handler;

    public DeleteGuildHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildNotifierMock = new Mock<IGuildNotifier>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _guildNotifierMock
            .Setup(x => x.NotifyGuildDeletedAsync(It.IsAny<GuildDeletedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new DeleteGuildHandler(
            _guildRepositoryMock.Object,
            _guildNotifierMock.Object,
            new UploadedFileCleanupService(
                _uploadedFileRepositoryMock.Object,
                _objectStorageServiceMock.Object,
                NullLogger<UploadedFileCleanupService>.Instance),
            _unitOfWorkMock.Object,
            NullLogger<DeleteGuildHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(guildId, callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotOwner_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(guild.Id, callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenOwnerDeletesGuild_ShouldDeleteGuildAndCommit()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var ownerId = guild.OwnerUserId;

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(guild.Id, ownerId);

        response.Success.Should().BeTrue();
        _guildRepositoryMock.Verify(
            x => x.DeleteAsync(guild.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _guildNotifierMock.Verify(
            x => x.NotifyGuildDeletedAsync(
                It.Is<GuildDeletedNotification>(notification => notification.GuildId == guild.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenOwnerDeletesGuild_ShouldNotifyAfterCommit()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var ownerId = guild.OwnerUserId;
        var sequence = new MockSequence();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.DeleteAsync(guild.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .InSequence(sequence)
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _guildNotifierMock
            .InSequence(sequence)
            .Setup(x => x.NotifyGuildDeletedAsync(
                It.Is<GuildDeletedNotification>(notification => notification.GuildId == guild.Id),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(guild.Id, ownerId);

        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenGuildHasIconFile_ShouldDeleteStoredObjectAfterCommit()
    {
        var iconFileId = UploadedFileId.From(Guid.Parse("b0c7172f-7770-4c05-af10-2ac1a3381995"));
        var guild = ApplicationTestBuilders.CreateGuild(iconFileId: iconFileId);
        var ownerId = guild.OwnerUserId;
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(fileName: "guild-icon.png", storageKey: "uploads/2026/03/icon.png", contentType: "image/png", sizeBytes: 128, purpose: UploadPurpose.GuildIcon);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(iconFileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        var response = await _handler.HandleAsync(guild.Id, ownerId);

        response.Success.Should().BeTrue();
        _uploadedFileRepositoryMock.Verify(
            x => x.DeleteAsync(iconFileId, It.IsAny<CancellationToken>()),
            Times.Once);
        _objectStorageServiceMock.Verify(
            x => x.DeleteIfExistsAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotificationFails_ShouldStillDeleteGuild()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var ownerId = guild.OwnerUserId;

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildNotifierMock
            .Setup(x => x.NotifyGuildDeletedAsync(It.IsAny<GuildDeletedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(guild.Id, ownerId);

        response.Success.Should().BeTrue();
        _guildRepositoryMock.Verify(
            x => x.DeleteAsync(guild.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
