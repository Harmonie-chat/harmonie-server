using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class MessageSendOrchestratorTests
{
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IMessageAttachmentRepository> _messageAttachmentRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly MessageSendOrchestrator _orchestrator;

    public MessageSendOrchestratorTests()
    {
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _messageAttachmentRepositoryMock = new Mock<IMessageAttachmentRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = _unitOfWorkMock.SetupTransactionMock();
        _userRepositoryMock = new Mock<IUserRepository>();

        _orchestrator = new MessageSendOrchestrator(
            _messageRepositoryMock.Object,
            _messageAttachmentRepositoryMock.Object,
            new MessageAttachmentResolver(_uploadedFileRepositoryMock.Object),
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    private static MessageScope AnyScope() => new MessageScope.Channel(GuildChannelId.New());
    private static UserId AnyUser() => UserId.New();

    // ── 1. Auth fails → error propagated ────────────────────────────────

    [Fact]
    public async Task SendAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scopeMock = new Mock<ISendMessageScope<ChannelSendMessageScope.Context>>();
        scopeMock
            .Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<ChannelSendMessageScope.Context>.Denied(
                new ApplicationError("AUTH_DENIED", "Not allowed")));

        var result = await _orchestrator.SendAsync(
            scopeMock.Object, AnyScope(), "hello", null, null, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
        result.Error.Detail.Should().Be("Not allowed");
    }

    // ── 2. Reply target wrong scope → NotFound ──────────────────────────

    [Fact]
    public async Task SendAsync_WhenReplyTargetScopeMismatch_ShouldReturnNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageScope = AnyScope();
        var replyTargetId = MessageId.New();
        var otherScope = new MessageScope.Conversation(ConversationId.New());

        _messageRepositoryMock
            .Setup(x => x.GetReplyTargetSummaryAsync(replyTargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplyTargetSummary(
                replyTargetId, otherScope, UserId.New(), "u", null, "content", false, false, null));

        var result = await _orchestrator.SendAsync(
            scope.Object, messageScope, "hello", null, replyTargetId.Value, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    // ── 2a. Reply target matches scope → included in result ───────────

    [Fact]
    public async Task SendAsync_WhenReplyTargetMatchesScope_ShouldIncludeReplyPreview()
    {
        var scope = CreateAuthorizedScope();
        var messageScope = AnyScope();
        var replyTargetId = MessageId.New();
        var authorId = UserId.New();

        _messageRepositoryMock
            .Setup(x => x.GetReplyTargetSummaryAsync(replyTargetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplyTargetSummary(
                replyTargetId, messageScope, authorId, "targetuser", "Target Display",
                "target content", true, false, null));

        _messageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.SendAsync(
            scope.Object, messageScope, "hello", null, replyTargetId.Value, null, AnyUser(),
            TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data!.ReplyTo.Should().NotBeNull();
        result.Data.ReplyTo!.MessageId.Should().Be(replyTargetId.Value);
        result.Data.ReplyTo.AuthorUsername.Should().Be("targetuser");
        result.Data.ReplyTo.Content.Should().Be("target content");
        result.Data.ReplyTo.HasAttachments.Should().BeTrue();
    }

    // ── 3. Attachments invalid → ValidationFailed ───────────────────────

    [Fact]
    public async Task SendAsync_WhenAttachmentResolutionFails_ShouldReturnValidationFailed()
    {
        var scope = CreateAuthorizedScope();
        var fileId = UploadedFileId.New();

        // Attachment not found → resolver returns failure
        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<UploadedFileId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UploadedFile>());

        var result = await _orchestrator.SendAsync(
            scope.Object, AnyScope(), "hello", [fileId.Value], null, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    // ── 4. Empty content + 0 attachments → ContentEmpty ─────────────────

    [Fact]
    public async Task SendAsync_WhenContentEmptyAndNoAttachments_ShouldReturnContentEmpty()
    {
        var scope = CreateAuthorizedScope();

        var result = await _orchestrator.SendAsync(
            scope.Object, AnyScope(), null, null, null, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
    }

    // ── 5. Happy path: side effects, notify, link previews called in order ──

    [Fact]
    public async Task SendAsync_WhenSuccessful_ShouldCallSideEffectsNotifyAndLinkPreviews()
    {
        var scopeMock = new Mock<ISendMessageScope<ChannelSendMessageScope.Context>>();
        var context = new ChannelSendMessageScope.Context(
            GuildChannelId.New(), "general", GuildId.New(), "MyGuild", "caller", "Caller Display");
        var callOrder = new List<string>();

        scopeMock
            .Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<ChannelSendMessageScope.Context>.Authorized(context));

        scopeMock
            .Setup(x => x.ApplyInTransactionSideEffectsAsync(context, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("sideEffects"))
            .Returns(Task.CompletedTask);

        scopeMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                context, It.IsAny<Message>(), It.IsAny<IReadOnlyList<MessageAttachmentDto>>(), null, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("notify"))
            .Returns(Task.CompletedTask);

        scopeMock
            .Setup(x => x.ScheduleLinkPreviewResolution(
                context, It.IsAny<Message>(), It.IsAny<IReadOnlyList<Uri>>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("linkPreviews"));

        _messageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.SendAsync(
            scopeMock.Object, AnyScope(), "https://example.com", null, null, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Content.Should().Be("https://example.com");

        _messageRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(
            x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        callOrder.Should().Equal("sideEffects", "commit", "notify", "linkPreviews");
    }

    // ── 6. Link previews only triggered when URLs present ────────────────

    [Fact]
    public async Task SendAsync_WhenNoUrlsInContent_ShouldNotScheduleLinkPreviews()
    {
        var scopeMock = new Mock<ISendMessageScope<ChannelSendMessageScope.Context>>();
        var context = new ChannelSendMessageScope.Context(
            GuildChannelId.New(), "general", GuildId.New(), "MyGuild", "caller", "Display");

        scopeMock
            .Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<ChannelSendMessageScope.Context>.Authorized(context));

        scopeMock
            .Setup(x => x.ApplyInTransactionSideEffectsAsync(context, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        scopeMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                context, It.IsAny<Message>(), It.IsAny<IReadOnlyList<MessageAttachmentDto>>(), null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _messageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _orchestrator.SendAsync(
            scopeMock.Object, AnyScope(), "hello world, no urls here", null, null, null, AnyUser(), TestContext.Current.CancellationToken);

        scopeMock.Verify(
            x => x.ScheduleLinkPreviewResolution(
                context, It.IsAny<Message>(), It.IsAny<IReadOnlyList<Uri>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Mock<ISendMessageScope<ChannelSendMessageScope.Context>> CreateAuthorizedScope()
    {
        var scopeMock = new Mock<ISendMessageScope<ChannelSendMessageScope.Context>>();
        scopeMock
            .Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<ChannelSendMessageScope.Context>.Authorized(
                new ChannelSendMessageScope.Context(
                    GuildChannelId.New(), "general", GuildId.New(), "MyGuild", "caller", "Display")));
        scopeMock
            .Setup(x => x.ApplyInTransactionSideEffectsAsync(It.IsAny<ChannelSendMessageScope.Context>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        scopeMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<ChannelSendMessageScope.Context>(), It.IsAny<Message>(), It.IsAny<IReadOnlyList<MessageAttachmentDto>>(), It.IsAny<ReplyPreviewDto?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        scopeMock
            .Setup(x => x.ScheduleLinkPreviewResolution(
                It.IsAny<ChannelSendMessageScope.Context>(), It.IsAny<Message>(), It.IsAny<IReadOnlyList<Uri>>(), It.IsAny<CancellationToken>()));
        return scopeMock;
    }
}
