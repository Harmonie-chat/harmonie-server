using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.SearchMessages;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class SearchMessagesHandlerTests
{
    private readonly Mock<IGuildRepository> _guildRepositoryMock;
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IChannelMessageRepository> _channelMessageRepositoryMock;
    private readonly SearchMessagesHandler _handler;

    public SearchMessagesHandlerTests()
    {
        _guildRepositoryMock = new Mock<IGuildRepository>();
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _channelMessageRepositoryMock = new Mock<IChannelMessageRepository>();

        _handler = new SearchMessagesHandler(
            _guildRepositoryMock.Object,
            _guildChannelRepositoryMock.Object,
            _channelMessageRepositoryMock.Object,
            NullLogger<SearchMessagesHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenCursorIsInvalid_ShouldReturnValidationFailure()
    {
        var ownerId = UserId.New();
        var guild = CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        var response = await _handler.HandleAsync(
            guild.Id,
            new SearchMessagesRequest
            {
                Q = "deploy",
                Cursor = "invalid-cursor"
            },
            ownerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task HandleAsync_WhenGuildDoesNotExist_ShouldReturnNotFound()
    {
        var guildId = GuildId.New();
        var currentUserId = UserId.New();

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guildId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildAccessContext?)null);

        var response = await _handler.HandleAsync(
            guildId,
            new SearchMessagesRequest { Q = "deploy" },
            currentUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotGuildMember_ShouldReturnAccessDenied()
    {
        var ownerId = UserId.New();
        var currentUserId = UserId.New();
        var guild = CreateGuild(ownerId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, CallerRole: null));

        var response = await _handler.HandleAsync(
            guild.Id,
            new SearchMessagesRequest { Q = "deploy" },
            currentUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelFilterIsVoice_ShouldReturnNotText()
    {
        var ownerId = UserId.New();
        var guild = CreateGuild(ownerId);
        var channel = CreateChannel(guild.Id, GuildChannelType.Voice, "voice-room");

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        var response = await _handler.HandleAsync(
            guild.Id,
            new SearchMessagesRequest
            {
                Q = "deploy",
                ChannelId = channel.Id.ToString()
            },
            ownerId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldReturnMappedItemsAndCursor()
    {
        var ownerId = UserId.New();
        var authorId = UserId.New();
        var guild = CreateGuild(ownerId);
        var channel = CreateChannel(guild.Id, GuildChannelType.Text, "deployments");
        var before = new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc);
        var after = before.AddHours(-1);
        var item = CreateSearchItem(
            channel.Id,
            authorId,
            channel.Name,
            "deploy finished",
            createdAtUtc: after.AddMinutes(30));
        var nextCursor = new ChannelMessageCursor(item.CreatedAtUtc, item.MessageId);

        _guildRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(guild.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAccessContext(guild, GuildRole.Admin));

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Admin));

        _channelMessageRepositoryMock
            .Setup(x => x.SearchGuildMessagesAsync(
                It.Is<SearchGuildMessagesQuery>(query =>
                    query.GuildId == guild.Id
                    && query.SearchText == "deploy"
                    && query.ChannelId == channel.Id
                    && query.AuthorId == authorId
                    && query.BeforeCreatedAtUtc == before
                    && query.AfterCreatedAtUtc == after),
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchGuildMessagesPage([item], nextCursor));

        var response = await _handler.HandleAsync(
            guild.Id,
            new SearchMessagesRequest
            {
                Q = " deploy ",
                ChannelId = channel.Id.ToString(),
                AuthorId = authorId.ToString(),
                Before = before.ToString("O"),
                After = after.ToString("O"),
                Limit = 10
            },
            ownerId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.GuildId.Should().Be(guild.Id.ToString());
        response.Data.Items.Should().ContainSingle();
        response.Data.Items[0].ChannelName.Should().Be("deployments");
        response.Data.Items[0].AuthorUserId.Should().Be(authorId.ToString());
        response.Data.Items[0].Content.Should().Be("deploy finished");
        response.Data.NextCursor.Should().NotBeNullOrWhiteSpace();
    }

    private static Guild CreateGuild(UserId ownerId)
    {
        var nameResult = GuildName.Create("Search Guild");
        if (nameResult.IsFailure || nameResult.Value is null)
            throw new InvalidOperationException("Failed to create test guild name.");

        var guildResult = Guild.Create(nameResult.Value, ownerId);
        if (guildResult.IsFailure || guildResult.Value is null)
            throw new InvalidOperationException("Failed to create test guild.");

        return guildResult.Value;
    }

    private static GuildChannel CreateChannel(GuildId guildId, GuildChannelType type, string name)
    {
        var channelResult = GuildChannel.Create(
            guildId,
            name,
            type,
            isDefault: false,
            position: 1);
        if (channelResult.IsFailure || channelResult.Value is null)
            throw new InvalidOperationException("Failed to create test channel.");

        return channelResult.Value;
    }

    private static SearchGuildMessagesItem CreateSearchItem(
        GuildChannelId channelId,
        UserId authorUserId,
        string channelName,
        string content,
        DateTime createdAtUtc)
    {
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Failed to create test message content.");

        return new SearchGuildMessagesItem(
            MessageId: ChannelMessageId.New(),
            ChannelId: channelId,
            ChannelName: channelName,
            AuthorUserId: authorUserId,
            AuthorUsername: "author-user",
            AuthorDisplayName: "Author Display",
            Content: contentResult.Value,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: null);
    }
}
