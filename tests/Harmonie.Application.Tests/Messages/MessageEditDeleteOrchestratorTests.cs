using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class MessageEditDeleteOrchestratorTests
{
    // Simple test context used across both channel and conversation scope mocks.
    public sealed record OrchestratorTestContext : ScopeContext;

    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IMessageAttachmentRepository> _messageAttachmentRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IObjectStorageService> _objectStorageServiceMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly MessageEditDeleteOrchestrator _orchestrator;

    private static readonly OrchestratorTestContext Ctx = new();

    public MessageEditDeleteOrchestratorTests()
    {
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _messageAttachmentRepositoryMock = new Mock<IMessageAttachmentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = _unitOfWorkMock.SetupTransactionMock();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _objectStorageServiceMock = new Mock<IObjectStorageService>();
        _userRepositoryMock = new Mock<IUserRepository>();

        _messageRepositoryMock
            .Setup(x => x.GetMentionedUserIdsByMessageIdAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Guid>>());

        var uploadedFileCleanupService = new UploadedFileCleanupService(
            _uploadedFileRepositoryMock.Object,
            _objectStorageServiceMock.Object,
            NullLogger<UploadedFileCleanupService>.Instance);

        _orchestrator = new MessageEditDeleteOrchestrator(
            _messageRepositoryMock.Object,
            _messageAttachmentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            uploadedFileCleanupService,
            TestTime.CreateProvider());
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static MessageScope ChannelScope() => new MessageScope.Channel(GuildChannelId.New());
    private static MessageScope ConversationScope() => new MessageScope.Conversation(ConversationId.New());
    private static MessageId AnyMessageId() => MessageId.New();
    private static UserId AnyUser() => UserId.New();
    private static UploadedFileId AnyAttachmentId() => UploadedFileId.New();
    private const string TestContent = "updated content";

    /// <summary>Creates a scope mock that authorizes successfully and allows notifications.</summary>
    private static Mock<IMessageEditDeleteScope<OrchestratorTestContext>> CreateAuthorizedScope(
        bool canDeleteOthers = false)
    {
        var mock = new Mock<IMessageEditDeleteScope<OrchestratorTestContext>>();
        mock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Authorized(Ctx));
        mock.Setup(x => x.CanDeleteOthersMessages(Ctx)).Returns(canDeleteOthers);
        mock.Setup(x => x.NotifyMessageUpdatedAsync(
            Ctx, It.IsAny<MessageId>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.NotifyMessageDeletedAsync(
            Ctx, It.IsAny<MessageId>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    /// <summary>Creates a scope mock that denies authorization with the given error code.</summary>
    private static Mock<IMessageEditDeleteScope<OrchestratorTestContext>> CreateDeniedScope(string code, string detail)
    {
        var mock = new Mock<IMessageEditDeleteScope<OrchestratorTestContext>>();
        mock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Denied(new ApplicationError(code, detail)));
        return mock;
    }

    /// <summary>Registers a message in the mock repository and returns it (its Id may differ from messageId).</summary>
    private Message SetupMessageExists(MessageId messageId, MessageScope scope, UserId? authorId = null)
    {
        Message message;
        if (scope is MessageScope.Channel c)
            message = ApplicationTestBuilders.CreateChannelMessage(c.ChannelId, authorId ?? AnyUser(), "original");
        else
            message = ApplicationTestBuilders.CreateConversationMessage(
                ((MessageScope.Conversation)scope).ConversationId, authorId ?? AnyUser(), "original");
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        return message;
    }

    private void SetupMessageNotFound(MessageId messageId)
    {
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EditAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EditAsync_WithEmptyContent_ShouldReturnContentEmpty()
    {
        var scope = CreateAuthorizedScope();
        var result = await _orchestrator.EditAsync(
            scope.Object, ChannelScope(), AnyMessageId(), "   ", null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scope = CreateDeniedScope("AUTH_DENIED", "Not allowed");
        var result = await _orchestrator.EditAsync(
            scope.Object, ChannelScope(), AnyMessageId(), TestContent, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    [Fact]
    public async Task EditAsync_WhenMessageNotFound_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        SetupMessageNotFound(messageId);

        var result = await _orchestrator.EditAsync(
            scope.Object, ChannelScope(), messageId, TestContent, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task EditAsync_WhenMessageInWrongScope_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var wrongScope = new MessageScope.Conversation(ConversationId.New());
        SetupMessageExists(messageId, wrongScope);

        var result = await _orchestrator.EditAsync(
            scope.Object, ChannelScope(), messageId, TestContent, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task EditAsync_WhenCallerIsNotAuthor_ShouldReturnEditForbidden()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var callerId = UserId.New();
        SetupMessageExists(messageId, messageScope, authorId);

        var result = await _orchestrator.EditAsync(
            scope.Object, messageScope, messageId, TestContent, null, callerId, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.EditForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EditAsync_WhenAuthorEditsOwnMessage_ShouldPersistCommitAndNotify()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var message = SetupMessageExists(messageId, messageScope, authorId);
        _messageAttachmentRepositoryMock
            .Setup(x => x.GetByMessageIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MessageAttachment>());

        var callOrder = new List<string>();
        _messageRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("update"))
            .Returns(Task.CompletedTask);
        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);
        scopeMock
            .Setup(x => x.NotifyMessageUpdatedAsync(
                Ctx, message.Id, TestContent, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("notify"))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.EditAsync(
            scopeMock.Object, messageScope, messageId, TestContent, null, authorId, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data!.Content.Should().Be(TestContent);
        result.Data.UpdatedAtUtc.Should().NotBeNull();
        _messageRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        scopeMock.Verify(
            x => x.NotifyMessageUpdatedAsync(Ctx, message.Id, TestContent, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // UpdatedAtUtc is validated before commit → callOrder is: update, commit, notify
        callOrder.Should().Equal("update", "commit", "notify");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DeleteAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scope = CreateDeniedScope("AUTH_DENIED", "Not allowed");
        var result = await _orchestrator.DeleteAsync(
            scope.Object, ChannelScope(), AnyMessageId(), AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    [Fact]
    public async Task DeleteAsync_WhenMessageNotFound_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        SetupMessageNotFound(messageId);

        var result = await _orchestrator.DeleteAsync(
            scope.Object, ChannelScope(), messageId, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenMessageInWrongScope_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var wrongScope = new MessageScope.Conversation(ConversationId.New());
        SetupMessageExists(messageId, wrongScope);

        var result = await _orchestrator.DeleteAsync(
            scope.Object, ChannelScope(), messageId, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenAuthorDeletesOwnMessage_ShouldPersistCommitAndNotify()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var message = SetupMessageExists(messageId, messageScope, authorId);

        var callOrder = new List<string>();
        _messageRepositoryMock
            .Setup(x => x.SoftDeleteAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("softDelete"))
            .Returns(Task.CompletedTask);
        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);
        scopeMock
            .Setup(x => x.NotifyMessageDeletedAsync(Ctx, message.Id, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("notify"))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.DeleteAsync(
            scopeMock.Object, messageScope, messageId, authorId, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _messageRepositoryMock.Verify(x => x.SoftDeleteAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        scopeMock.Verify(
            x => x.NotifyMessageDeletedAsync(Ctx, message.Id, It.IsAny<CancellationToken>()), Times.Once);
        callOrder.Should().Equal("softDelete", "commit", "notify");
    }

    [Fact]
    public async Task DeleteAsync_WhenAdminInChannel_ShouldDeleteOthersMessage()
    {
        // Admin override: CanDeleteOthersMessages = true
        var scopeMock = CreateAuthorizedScope(canDeleteOthers: true);
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var adminId = UserId.New();
        SetupMessageExists(messageId, messageScope, authorId);

        _messageRepositoryMock
            .Setup(x => x.SoftDeleteAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.DeleteAsync(
            scopeMock.Object, messageScope, messageId, adminId, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _messageRepositoryMock.Verify(x => x.SoftDeleteAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNonAdminInChannel_ShouldReturnDeleteForbidden()
    {
        // Non-admin cannot delete others' messages: CanDeleteOthersMessages = false
        var scope = CreateAuthorizedScope(canDeleteOthers: false);
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var memberId = UserId.New();
        SetupMessageExists(messageId, messageScope, authorId);

        var result = await _orchestrator.DeleteAsync(
            scope.Object, messageScope, messageId, memberId, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenAuthorInConversation_ShouldDeleteOwnMessage()
    {
        var scopeMock = CreateAuthorizedScope(canDeleteOthers: false);
        var messageId = AnyMessageId();
        var messageScope = ConversationScope();
        var authorId = UserId.New();
        SetupMessageExists(messageId, messageScope, authorId);

        _messageRepositoryMock
            .Setup(x => x.SoftDeleteAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.DeleteAsync(
            scopeMock.Object, messageScope, messageId, authorId, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _messageRepositoryMock.Verify(x => x.SoftDeleteAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNonAuthorInConversation_ShouldReturnDeleteForbidden()
    {
        // Conversations never allow non-author deletion: CanDeleteOthersMessages = false
        var scope = CreateAuthorizedScope(canDeleteOthers: false);
        var messageId = AnyMessageId();
        var messageScope = ConversationScope();
        var authorId = UserId.New();
        var otherUserId = UserId.New();
        SetupMessageExists(messageId, messageScope, authorId);

        var result = await _orchestrator.DeleteAsync(
            scope.Object, messageScope, messageId, otherUserId, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DeleteAttachmentAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAttachmentAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scope = CreateDeniedScope("AUTH_DENIED", "Not allowed");
        var result = await _orchestrator.DeleteAttachmentAsync(
            scope.Object, ChannelScope(), AnyMessageId(), AnyAttachmentId(), AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    [Fact]
    public async Task DeleteAttachmentAsync_WhenMessageNotFound_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        SetupMessageNotFound(messageId);

        var result = await _orchestrator.DeleteAttachmentAsync(
            scope.Object, ChannelScope(), messageId, AnyAttachmentId(), AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_WhenMessageInWrongScope_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var wrongScope = new MessageScope.Conversation(ConversationId.New());
        SetupMessageExists(messageId, wrongScope);

        var result = await _orchestrator.DeleteAttachmentAsync(
            scope.Object, ChannelScope(), messageId, AnyAttachmentId(), AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_WhenCallerIsNotAuthor_ShouldReturnDeleteForbidden()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var callerId = UserId.New();
        SetupMessageExists(messageId, messageScope, authorId);

        var result = await _orchestrator.DeleteAttachmentAsync(
            scope.Object, messageScope, messageId, AnyAttachmentId(), callerId, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_WhenAttachmentNotFound_ShouldReturnAttachmentNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var attachmentId = AnyAttachmentId();
        SetupMessageExists(messageId, messageScope, authorId);

        _messageAttachmentRepositoryMock
            .Setup(x => x.DeleteAsync(messageId, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _orchestrator.DeleteAttachmentAsync(
            scope.Object, messageScope, messageId, attachmentId, authorId, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.AttachmentNotFound);
    }

    [Fact]
    public async Task DeleteAttachmentAsync_WhenAuthorDeletesAttachment_ShouldDeleteCommitAndCleanupFile()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var attachmentId = AnyAttachmentId();
        SetupMessageExists(messageId, messageScope, authorId);

        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(
            id: attachmentId, uploaderUserId: authorId, storageKey: "attachments/file.txt");

        _messageAttachmentRepositoryMock
            .Setup(x => x.DeleteAsync(messageId, attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .Setup(x => x.DeleteIfExistsAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _uploadedFileRepositoryMock
            .Setup(x => x.DeleteAsync(attachmentId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.DeleteAttachmentAsync(
            scope.Object, messageScope, messageId, attachmentId, authorId, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _messageAttachmentRepositoryMock.Verify(
            x => x.DeleteAsync(messageId, attachmentId, It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uploadedFileRepositoryMock.Verify(
            x => x.DeleteAsync(attachmentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Call order: delete reference → commit → cleanup file
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAttachmentAsync_ShouldDeleteThenCommitThenCleanupFile()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var attachmentId = AnyAttachmentId();
        SetupMessageExists(messageId, messageScope, authorId);

        var uploadedFile = ApplicationTestBuilders.CreateUploadedFile(
            id: attachmentId, uploaderUserId: authorId, storageKey: "attachments/file.txt");

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdAsync(attachmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedFile);

        _objectStorageServiceMock
            .Setup(x => x.DeleteIfExistsAsync(uploadedFile.StorageKey, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var callOrder = new List<string>();
        _messageAttachmentRepositoryMock
            .Setup(x => x.DeleteAsync(messageId, attachmentId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("deleteRef"))
            .ReturnsAsync(true);
        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);

        // Track transaction dispose to verify cleanup happens after the using block exits.
        _transactionMock.As<IAsyncDisposable>()
            .Setup(x => x.DisposeAsync())
            .Callback(() => callOrder.Add("txDispose"))
            .Returns(ValueTask.CompletedTask);

        // Track cleanup by watching DeleteAsync on the uploaded file repo.
        _uploadedFileRepositoryMock
            .Setup(x => x.DeleteAsync(attachmentId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("cleanup"))
            .Returns(Task.CompletedTask);

        await _orchestrator.DeleteAttachmentAsync(
            scope.Object, messageScope, messageId, attachmentId, authorId, TestContext.Current.CancellationToken);

        // Cleanup must happen outside the transaction: delete → commit → dispose → cleanup
        callOrder.Should().Equal("deleteRef", "commit", "txDispose", "cleanup");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EditAsync — mentions

    [Fact]
    public async Task EditAsync_WithValidMentions_ShouldReplaceAndReturnMentions()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        var mentionUser = UserId.New();
        SetupMessageExists(messageId, messageScope, authorId);

        _messageAttachmentRepositoryMock
            .Setup(x => x.GetByMessageIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MessageAttachment>());

        _userRepositoryMock
            .Setup(x => x.GetManyByIdsAsync(
                It.IsAny<IReadOnlyList<UserId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ApplicationTestBuilders.CreateUser(mentionUser) });

        scopeMock
            .Setup(x => x.ValidateMentionedUsersAsync(
                It.IsAny<IReadOnlyCollection<UserId>>(), Ctx, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _messageRepositoryMock
            .Setup(x => x.ReplaceMentionsAsync(
                messageId, It.IsAny<IReadOnlyCollection<UserId>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.EditAsync(
            scopeMock.Object, messageScope, messageId, TestContent,
            new List<Guid> { mentionUser.Value }, authorId, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data!.MentionedUserIds.Should().ContainSingle().Which.Should().Be(mentionUser.Value);
    }

    [Fact]
    public async Task EditAsync_WithEmptyMentionList_ShouldClearMentions()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        SetupMessageExists(messageId, messageScope, authorId);

        _messageAttachmentRepositoryMock
            .Setup(x => x.GetByMessageIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MessageAttachment>());

        _messageRepositoryMock
            .Setup(x => x.ReplaceMentionsAsync(
                It.IsAny<MessageId>(), It.Is<IReadOnlyCollection<UserId>>(c => c.Count == 0), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.EditAsync(
            scopeMock.Object, messageScope, messageId, TestContent,
            Array.Empty<Guid>(), authorId, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data!.MentionedUserIds.Should().BeEmpty();
        _messageRepositoryMock.Verify(
            x => x.ReplaceMentionsAsync(
                It.IsAny<MessageId>(), It.IsAny<IReadOnlyCollection<UserId>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EditAsync_WithNullMentions_ShouldNotTouchMentions()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var authorId = UserId.New();
        SetupMessageExists(messageId, messageScope, authorId);

        _messageAttachmentRepositoryMock
            .Setup(x => x.GetByMessageIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MessageAttachment>());

        _messageRepositoryMock
            .Setup(x => x.GetMentionedUserIdsByMessageIdAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Guid>>());

        var result = await _orchestrator.EditAsync(
            scopeMock.Object, messageScope, messageId, TestContent,
            null, authorId, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data!.MentionedUserIds.Should().BeEmpty();
        _messageRepositoryMock.Verify(
            x => x.ReplaceMentionsAsync(
                It.IsAny<MessageId>(), It.IsAny<IReadOnlyCollection<UserId>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
