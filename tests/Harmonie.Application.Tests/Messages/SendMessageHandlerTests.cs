using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Guilds;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class SendMessageHandlerTests
{
    private readonly Mock<IGuildChannelRepository> _guildChannelRepositoryMock;
    private readonly Mock<IMessageRepository> _channelMessageRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<ITextChannelNotifier> _textChannelNotifierMock;
    private readonly SendMessageHandler _handler;

    public SendMessageHandlerTests()
    {
        _guildChannelRepositoryMock = new Mock<IGuildChannelRepository>();
        _channelMessageRepositoryMock = new Mock<IMessageRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _textChannelNotifierMock = new Mock<ITextChannelNotifier>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _textChannelNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<TextChannelMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new SendMessageHandler(
            _guildChannelRepositoryMock.Object,
            _channelMessageRepositoryMock.Object,
            new MessageAttachmentResolver(_uploadedFileRepositoryMock.Object),
            _unitOfWorkMock.Object,
            _textChannelNotifierMock.Object,
            NullLogger<SendMessageHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var channelId = GuildChannelId.New();
        var userId = UserId.New();
        var request = new SendMessageRequest("hello");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channelId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChannelAccessContext?)null);

        var response = await _handler.HandleAsync(channelId, request, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenChannelIsVoice_ShouldReturnNotText()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Voice);
        var userId = UserId.New();
        var request = new SendMessageRequest("hello");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        var response = await _handler.HandleAsync(channel.Id, request, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotMember_ShouldReturnAccessDenied()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();
        var request = new SendMessageRequest("hello");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, CallerRole: null));

        var response = await _handler.HandleAsync(channel.Id, request, userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_ShouldReturnMessageContentEmpty()
    {
        var response = await _handler.HandleAsync(
            GuildChannelId.New(),
            new SendMessageRequest("   "),
            UserId.New());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldPersistTrimmedContentCommitAndNotify()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();
        var request = new SendMessageRequest("  hello team  ");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        Message? persistedMessage = null;
        _channelMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(channel.Id, request, userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Content.Should().Be("hello team");
        response.Data.Attachments.Should().BeEmpty();
        persistedMessage.Should().NotBeNull();
        persistedMessage!.Content.Value.Should().Be("hello team");
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.DisposeAsync(), Times.Once);
        _textChannelNotifierMock.Verify(
            x => x.NotifyMessageCreatedAsync(
                It.Is<TextChannelMessageCreatedNotification>(n =>
                    n.ChannelId == channel.Id
                    && n.GuildId == channel.GuildId
                    && n.AuthorUserId == userId
                    && n.Content == "hello team"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithOwnedAttachmentFiles_ShouldPersistAttachmentsAndReturnThem()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();
        var attachment = ApplicationTestBuilders.CreateUploadedFile(uploaderUserId: userId, fileName: "report.pdf", contentType: "application/pdf");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<UploadedFileId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([attachment]);

        Message? persistedMessage = null;
        _channelMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            channel.Id,
            new SendMessageRequest("message with file", [attachment.Id.ToString()]),
            userId);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Attachments.Should().ContainSingle();
        response.Data.Attachments[0].FileId.Should().Be(attachment.Id.ToString());
        persistedMessage.Should().NotBeNull();
        persistedMessage!.Attachments.Should().ContainSingle();
        persistedMessage.Attachments[0].FileId.Should().Be(attachment.Id);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();
        var request = new SendMessageRequest("hello");

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _textChannelNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<TextChannelMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(channel.Id, request, userId);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestTokenCanceledAfterCommit_ShouldNotifyWithIndependentToken()
    {
        var channel = ApplicationTestBuilders.CreateChannel(GuildChannelType.Text);
        var userId = UserId.New();
        var request = new SendMessageRequest("hello");
        using var requestCts = new CancellationTokenSource();
        var notifierToken = CancellationToken.None;

        _guildChannelRepositoryMock
            .Setup(x => x.GetWithCallerRoleAsync(channel.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChannelAccessContext(channel, GuildRole.Member));

        _channelMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => requestCts.Cancel())
            .Returns(Task.CompletedTask);

        _textChannelNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<TextChannelMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .Callback<TextChannelMessageCreatedNotification, CancellationToken>((_, token) => notifierToken = token)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(channel.Id, request, userId, requestCts.Token);

        response.Success.Should().BeTrue();
        notifierToken.Equals(requestCts.Token).Should().BeFalse();
        notifierToken.IsCancellationRequested.Should().BeFalse();
        _textChannelNotifierMock.Verify(
            x => x.NotifyMessageCreatedAsync(
                It.IsAny<TextChannelMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

}
