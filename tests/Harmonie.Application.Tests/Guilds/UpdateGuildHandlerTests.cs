using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Guilds.UpdateGuild;
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

public sealed class UpdateGuildHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly UpdateGuildHandler _handler;

    public UpdateGuildHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _handler = new UpdateGuildHandler(
            _guildRepositoryMock.Object,
            new UploadedFileCleanupService(
                _uploadedFileRepositoryMock.Object,
                _objectStorageServiceMock.Object,
                NullLogger<UploadedFileCleanupService>.Instance),
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

        var response = await _handler.HandleAsync(new UpdateGuildInput(guildId, null, null, null, null, null, false, false, false, false, false), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdminNorOwner_ShouldReturnAccessDenied()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(new UpdateGuildInput(guild.Id, null, null, null, null, null, false, false, false, false, false), callerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminUpdatesGuild_ShouldPersistAndReturnIcon()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var request = new UpdateGuildRequest
        {
            Name = "Updated Guild",
            NameIsSet = true,
            IconFileId = "1c73fa0b-0a39-4ea8-b43e-48c703bbf5fe",
            IconFileIdIsSet = true,
            IconIsSet = true,
            IconColor = "#7C3AED",
            IconColorIsSet = true,
            IconName = "sword",
            IconNameIsSet = true,
            IconBg = "#1F2937",
            IconBgIsSet = true
        };

        var response = await _handler.HandleAsync(new UpdateGuildInput(guild.Id, request.Name, request.IconFileId, request.IconColor, request.IconName, request.IconBg, request.NameIsSet, request.IconFileIdIsSet, request.IconColorIsSet, request.IconNameIsSet, request.IconBgIsSet), adminId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().Be("Updated Guild");
        response.Data.IconFileId.Should().Be("1c73fa0b-0a39-4ea8-b43e-48c703bbf5fe");
        response.Data.Icon.Should().NotBeNull();
        response.Data.Icon!.Name.Should().Be("sword");

        _guildRepositoryMock.Verify(
            x => x.UpdateAsync(
                It.Is<Guild>(updatedGuild =>
                    updatedGuild.Name.Value == "Updated Guild"
                    && updatedGuild.IconFileId == UploadedFileId.From(Guid.Parse("1c73fa0b-0a39-4ea8-b43e-48c703bbf5fe"))
                    && updatedGuild.IconColor == "#7C3AED"
                    && updatedGuild.IconName == "sword"
                    && updatedGuild.IconBg == "#1F2937"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenIconIsCleared_ShouldSetIconPayloadToNull()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        guild.UpdateIconColor("#INITIAL");
        guild.UpdateIconName("shield");
        guild.UpdateIconBg("#000000");

        var ownerId = guild.OwnerUserId;

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var request = new UpdateGuildRequest
        {
            IconIsSet = true,
            IconColor = null,
            IconColorIsSet = true,
            IconName = null,
            IconNameIsSet = true,
            IconBg = null,
            IconBgIsSet = true
        };

        var response = await _handler.HandleAsync(new UpdateGuildInput(guild.Id, request.Name, request.IconFileId, request.IconColor, request.IconName, request.IconBg, request.NameIsSet, request.IconFileIdIsSet, request.IconColorIsSet, request.IconNameIsSet, request.IconBgIsSet), ownerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Icon.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenNoFieldsSet_ShouldNotPersist()
    {
        var guild = ApplicationTestBuilders.CreateGuild();
        var ownerId = guild.OwnerUserId;

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(new UpdateGuildInput(guild.Id, null, null, null, null, null, false, false, false, false, false), ownerId);

        response.Success.Should().BeTrue();
        _guildRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            x => x.BeginAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenReplacingExistingIconFile_ShouldDeleteOldStoredObject()
    {
        var oldFileId = UploadedFileId.From(Guid.Parse("08f8d69f-5b34-4037-8fb0-ccf6d98af75d"));
        var guild = ApplicationTestBuilders.CreateGuild(iconFileId: oldFileId);
        var ownerId = guild.OwnerUserId;
        var oldUploadedFile = ApplicationTestBuilders.CreateUploadedFile(fileName: "guild-icon-old.png", storageKey: "guild-icons/old-file.png", contentType: "image/png", sizeBytes: 123, purpose: UploadPurpose.GuildIcon);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(oldFileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldUploadedFile);

        var request = new UpdateGuildRequest
        {
            IconFileId = "5d2bd47d-c897-4eca-8aec-e5e68217e1d9",
            IconFileIdIsSet = true
        };

        var response = await _handler.HandleAsync(new UpdateGuildInput(guild.Id, request.Name, request.IconFileId, request.IconColor, request.IconName, request.IconBg, request.NameIsSet, request.IconFileIdIsSet, request.IconColorIsSet, request.IconNameIsSet, request.IconBgIsSet), ownerId);

        response.Success.Should().BeTrue();
        _objectStorageServiceMock.Verify(
            x => x.DeleteIfExistsAsync(oldUploadedFile.StorageKey, It.IsAny<CancellationToken>()),
            Times.Once);
        _uploadedFileRepositoryMock.Verify(
            x => x.DeleteAsync(oldFileId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenClearingExistingIconFile_ShouldDeleteOldStoredObject()
    {
        var oldFileId = UploadedFileId.From(Guid.Parse("08f8d69f-5b34-4037-8fb0-ccf6d98af75d"));
        var guild = ApplicationTestBuilders.CreateGuild(iconFileId: oldFileId);
        var ownerId = guild.OwnerUserId;
        var oldUploadedFile = ApplicationTestBuilders.CreateUploadedFile(fileName: "guild-icon-old.png", storageKey: "guild-icons/old-file.png", contentType: "image/png", sizeBytes: 123, purpose: UploadPurpose.GuildIcon);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(oldFileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldUploadedFile);

        var request = new UpdateGuildRequest
        {
            IconFileId = null,
            IconFileIdIsSet = true
        };

        var response = await _handler.HandleAsync(new UpdateGuildInput(guild.Id, request.Name, request.IconFileId, request.IconColor, request.IconName, request.IconBg, request.NameIsSet, request.IconFileIdIsSet, request.IconColorIsSet, request.IconNameIsSet, request.IconBgIsSet), ownerId);

        response.Success.Should().BeTrue();
        _uploadedFileRepositoryMock.Verify(
            x => x.DeleteAsync(oldFileId, It.IsAny<CancellationToken>()),
            Times.Once);
        _objectStorageServiceMock.Verify(
            x => x.DeleteIfExistsAsync(oldUploadedFile.StorageKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
