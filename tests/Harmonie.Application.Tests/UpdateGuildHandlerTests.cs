using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.UpdateGuild;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class UpdateGuildHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly UpdateGuildHandler _handler;

    public UpdateGuildHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
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

        _handler = new UpdateGuildHandler(
            _guildRepositoryMock.Object,
            _userRepositoryMock.Object,
            _uploadedFileRepositoryMock.Object,
            _objectStorageServiceMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<UpdateGuildHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(guildId, callerId, new UpdateGuildRequest());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsMemberNotAdminNorOwner_ShouldReturnAccessDenied()
    {
        var guild = CreateGuild();
        var callerId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(guild.Id, callerId, new UpdateGuildRequest());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenAdminUpdatesGuild_ShouldPersistAndReturnIcon()
    {
        var guild = CreateGuild();
        var adminId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var request = new UpdateGuildRequest
        {
            Name = "Updated Guild",
            NameIsSet = true,
            IconUrl = "https://cdn.harmonie.chat/guild-updated.png",
            IconUrlIsSet = true,
            IconIsSet = true,
            IconColor = "#7C3AED",
            IconColorIsSet = true,
            IconName = "sword",
            IconNameIsSet = true,
            IconBg = "#1F2937",
            IconBgIsSet = true
        };

        var response = await _handler.HandleAsync(guild.Id, adminId, request);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().Be("Updated Guild");
        response.Data.IconUrl.Should().Be("https://cdn.harmonie.chat/guild-updated.png");
        response.Data.Icon.Should().NotBeNull();
        response.Data.Icon!.Name.Should().Be("sword");

        _guildRepositoryMock.Verify(
            x => x.UpdateAsync(
                It.Is<Guild>(updatedGuild =>
                    updatedGuild.Name.Value == "Updated Guild"
                    && updatedGuild.IconUrl == "https://cdn.harmonie.chat/guild-updated.png"
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
        var guild = CreateGuild();
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

        var response = await _handler.HandleAsync(guild.Id, ownerId, request);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Icon.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenNoFieldsSet_ShouldNotPersist()
    {
        var guild = CreateGuild();
        var ownerId = guild.OwnerUserId;

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        var response = await _handler.HandleAsync(guild.Id, ownerId, new UpdateGuildRequest());

        response.Success.Should().BeTrue();
        _guildRepositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            x => x.BeginAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenReplacingLocalIconUrlWithoutOtherReferences_ShouldDeleteOldStoredObject()
    {
        var guild = CreateGuild(iconUrl: "/api/files/08f8d69f-5b34-4037-8fb0-ccf6d98af75d");
        var ownerId = guild.OwnerUserId;
        var oldUploadedFile = CreateUploadedFile(
            "guild-icon-old.png",
            "guild-icons/old-file.png");

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _guildRepositoryMock
            .Setup(x => x.IsIconUrlReferencedByAnotherGuildAsync(guild.IconUrl!, guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepositoryMock
            .Setup(x => x.ExistsByAvatarUrlAsync(guild.IconUrl!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<UploadedFileId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldUploadedFile);

        var request = new UpdateGuildRequest
        {
            IconUrl = "/api/files/5d2bd47d-c897-4eca-8aec-e5e68217e1d9",
            IconUrlIsSet = true
        };

        var response = await _handler.HandleAsync(guild.Id, ownerId, request);

        response.Success.Should().BeTrue();
        _objectStorageServiceMock.Verify(
            x => x.DeleteIfExistsAsync(oldUploadedFile.StorageKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenPreviousLocalIconUrlIsStillReferenced_ShouldNotDeleteStoredObject()
    {
        var guild = CreateGuild(iconUrl: "/api/files/08f8d69f-5b34-4037-8fb0-ccf6d98af75d");
        var ownerId = guild.OwnerUserId;

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Member));

        _guildRepositoryMock
            .Setup(x => x.IsIconUrlReferencedByAnotherGuildAsync(guild.IconUrl!, guild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new UpdateGuildRequest
        {
            IconUrl = null,
            IconUrlIsSet = true
        };

        var response = await _handler.HandleAsync(guild.Id, ownerId, request);

        response.Success.Should().BeTrue();
        _uploadedFileRepositoryMock.Verify(
            x => x.GetByIdAsync(It.IsAny<UploadedFileId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _objectStorageServiceMock.Verify(
            x => x.DeleteIfExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Guild CreateGuild(string? iconUrl = null)
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
            iconUrl: iconUrl);
    }

    private static UploadedFile CreateUploadedFile(
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

        return uploadedFileResult.Value;
    }
}
