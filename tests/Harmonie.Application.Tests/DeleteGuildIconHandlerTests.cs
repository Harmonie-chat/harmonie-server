using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.DeleteGuildIcon;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class DeleteGuildIconHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly DeleteGuildIconHandler _handler;

    public DeleteGuildIconHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

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

        var response = await _handler.HandleAsync(guildId, callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdminNorOwner_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild(UploadedFileId.From(Guid.Parse("08f8d69f-5b34-4037-8fb0-ccf6d98af75d")));
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(guild.Id, callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildHasNoIcon_ShouldReturnNotFound()
    {
        var guild = CreateGuild();
        var ownerId = guild.OwnerUserId;

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(guild.Id, ownerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Upload.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenOwnerDeletesExistingIcon_ShouldClearIconAfterCommitAndCleanupStoredFile()
    {
        var iconFileId = UploadedFileId.From(Guid.Parse("08f8d69f-5b34-4037-8fb0-ccf6d98af75d"));
        var guild = CreateGuild(iconFileId);
        var ownerId = guild.OwnerUserId;
        var uploadedFile = CreateUploadedFile(
            iconFileId,
            "guild-icon-old.png",
            "guild-icons/old-file.png");
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

        var response = await _handler.HandleAsync(guild.Id, ownerId);

        response.Success.Should().BeTrue();
        guild.IconFileId.Should().BeNull();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uploadedFileRepositoryMock.Verify(
            x => x.DeleteAsync(iconFileId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Guild CreateGuild(UploadedFileId? iconFileId = null)
    {
        var guildNameResult = GuildName.Create("Guild Alpha");
        if (guildNameResult.IsFailure || guildNameResult.Value is null)
            throw new InvalidOperationException("Failed to create guild name for tests.");

        return Guild.Rehydrate(
            GuildId.New(),
            guildNameResult.Value,
            UserId.New(),
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddDays(-1),
            iconFileId: iconFileId);
    }

    private static UploadedFile CreateUploadedFile(
        UploadedFileId expectedId,
        string fileName,
        string storageKey)
    {
        var uploadedFileResult = UploadedFile.Create(
            UserId.New(),
            fileName,
            "image/png",
            123,
            storageKey,
            UploadPurpose.GuildIcon);

        if (uploadedFileResult.IsFailure || uploadedFileResult.Value is null)
            throw new InvalidOperationException("Failed to create uploaded file for tests.");

        return UploadedFile.Rehydrate(
            expectedId,
            uploadedFileResult.Value.UploaderUserId,
            uploadedFileResult.Value.FileName,
            uploadedFileResult.Value.ContentType,
            uploadedFileResult.Value.SizeBytes,
            uploadedFileResult.Value.StorageKey,
            uploadedFileResult.Value.Purpose,
            uploadedFileResult.Value.CreatedAtUtc);
    }
}
