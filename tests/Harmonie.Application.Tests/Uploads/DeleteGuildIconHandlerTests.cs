using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Guilds.DeleteGuildIcon;
using Harmonie.Application.Tests.Common;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Uploads;

public sealed class DeleteGuildIconHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IGuildNotifier> _guildNotifierMock;
    private readonly DeleteGuildIconHandler _handler;

    public DeleteGuildIconHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _guildNotifierMock = new Mock<IGuildNotifier>();

        _unitOfWorkMock
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _transactionMock
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _handler = new DeleteGuildIconHandler(
            _guildRepositoryMock.Object,
            new UploadedFileCleanupService(
                _uploadedFileRepositoryMock.Object,
                _objectStorageServiceMock.Object,
                NullLogger<UploadedFileCleanupService>.Instance),
            _unitOfWorkMock.Object,
            _guildNotifierMock.Object,
            NullLogger<DeleteGuildIconHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(new DeleteGuildIconInput(guildId), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdminNorOwner_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild(iconFileId: UploadedFileId.From(Guid.Parse("08f8d69f-5b34-4037-8fb0-ccf6d98af75d")));
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(new DeleteGuildIconInput(guild.Id), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildHasNoIcon_ShouldReturnNotFound()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var ownerId = guild.OwnerUserId;

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(new DeleteGuildIconInput(guild.Id), ownerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenOwnerDeletesExistingIcon_ShouldClearIconAfterCommitAndCleanupStoredFile()
    {
        var iconFileId = UploadedFileId.From(Guid.Parse("08f8d69f-5b34-4037-8fb0-ccf6d98af75d"));
        var guild = ApplicationTestBuilders.CreateGuild(iconFileId: iconFileId);
        var ownerId = guild.OwnerUserId;
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(id: iconFileId, fileName: "guild-icon-old.png", contentType: "image/png", sizeBytes: 123, storageKey: "guild-icons/old-file.png", purpose: UploadPurpose.GuildIcon);
        var sequence = new MockSequence();

        _guildRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _unitOfWorkMock
            .InSequence(sequence)
            .Setup(x => x.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _guildRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.UpdateAsync(
                It.Is<Guild>(updatedGuild =>
                    updatedGuild.Id == guild.Id &&
                    updatedGuild.IconFileId == null),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .InSequence(sequence)
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uploadedFileRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.GetByIdAsync(iconFileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .InSequence(sequence)
            .Setup(x => x.DeleteIfExistsAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uploadedFileRepositoryMock
            .InSequence(sequence)
            .Setup(x => x.DeleteAsync(iconFileId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(new DeleteGuildIconInput(guild.Id), ownerId);

        response.Success.Should().BeTrue();
        guild.IconFileId.Should().BeNull();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uploadedFileRepositoryMock.Verify(
            x => x.DeleteAsync(iconFileId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenIconDeleted_ShouldCallNotifyGuildUpdatedAsyncWithNullIconFileId()
    {
        var iconFileId = UploadedFileId.From(Guid.Parse("08f8d69f-5b34-4037-8fb0-ccf6d98af75d"));
        var guild = ApplicationTestBuilders.CreateGuild(iconFileId: iconFileId);
        var ownerId = guild.OwnerUserId;
        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(
            id: iconFileId,
            fileName: "guild-icon.png",
            contentType: "image/png",
            sizeBytes: 512,
            storageKey: "guild-icons/icon.png",
            purpose: UploadPurpose.GuildIcon);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(iconFileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _guildNotifierMock
            .Setup(x => x.NotifyGuildUpdatedAsync(It.IsAny<GuildUpdatedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(new DeleteGuildIconInput(guild.Id), ownerId);

        response.Success.Should().BeTrue();

        _guildNotifierMock.Verify(
            x => x.NotifyGuildUpdatedAsync(
                It.Is<GuildUpdatedNotification>(n =>
                    n.GuildId == guild.Id
                    && n.IconFileId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
