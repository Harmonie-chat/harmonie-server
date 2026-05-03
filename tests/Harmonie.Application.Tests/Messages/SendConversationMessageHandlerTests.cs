using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.SendMessage;
using Harmonie.Application.Services;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.Entities.Uploads;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class SendConversationMessageHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _directMessageRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<ILinkPreviewRepository> _linkPreviewRepositoryMock;
    private readonly Mock<ILinkPreviewFetcher> _linkPreviewFetcherMock;
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<IConversationMessageNotifier> _directMessageNotifierMock;
    private readonly SendMessageHandler _handler;

    public SendConversationMessageHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _directMessageRepositoryMock = new Mock<IMessageRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _linkPreviewRepositoryMock = new Mock<ILinkPreviewRepository>();
        _linkPreviewFetcherMock = new Mock<ILinkPreviewFetcher>();
        _directMessageNotifierMock = new Mock<IConversationMessageNotifier>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<ConversationMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeMock = new Mock<IServiceScope>();
        var scopeProviderMock = new Mock<IServiceProvider>();
        scopeProviderMock.Setup(s => s.GetService(typeof(ILinkPreviewRepository)))
            .Returns(_linkPreviewRepositoryMock.Object);
        scopeProviderMock.Setup(s => s.GetService(typeof(ILinkPreviewFetcher)))
            .Returns(_linkPreviewFetcherMock.Object);
        scopeProviderMock.Setup(s => s.GetService(typeof(IConversationMessageNotifier)))
            .Returns(_directMessageNotifierMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(scopeProviderMock.Object);

        _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeFactoryMock.Setup(f => f.CreateScope())
            .Returns(scopeMock.Object);

        _handler = new SendMessageHandler(
            _conversationRepositoryMock.Object,
            _directMessageRepositoryMock.Object,
            new MessageAttachmentResolver(_uploadedFileRepositoryMock.Object),
            _unitOfWorkMock.Object,
            _directMessageNotifierMock.Object,
            new LinkPreviewResolutionService(
                _serviceScopeFactoryMock.Object,
                NullLogger<LinkPreviewResolutionService>.Instance),
            _directMessageRepositoryMock.Object,
            NullLogger<SendMessageHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var conversationId = ConversationId.New();
        var userId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversationId, It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccess?)null);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversationId, "hello"),
            userId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotParticipant_ShouldReturnAccessDenied()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var outsider = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, outsider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: null));

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello"),
            outsider,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task HandleAsync_WithNoContentAndNoAttachments_ShouldReturnContentEmpty(string? rawContent)
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId)));

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, rawContent),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldPersistCommitAndNotify()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());
        Message? persistedMessage = null;

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId), CallerUsername: "sender", CallerDisplayName: "Sender Display"));

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "  hello dm  "),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Content.Should().Be("hello dm");
        response.Data.Attachments.Should().BeEmpty();
        persistedMessage.Should().NotBeNull();
        persistedMessage!.Content!.Value.Should().Be("hello dm");
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _directMessageNotifierMock.Verify(
            x => x.NotifyMessageCreatedAsync(
                It.Is<ConversationMessageCreatedNotification>(n =>
                    n.ConversationId == conversation.Id
                    && n.AuthorUserId == currentUserId
                    && n.Content == "hello dm"
                    && n.AuthorUsername == "sender"
                    && n.AuthorDisplayName == "Sender Display"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithOwnedAttachmentFiles_ShouldPersistAttachmentsAndReturnThem()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());
        var attachment = ApplicationTestBuilders.CreateUploadedFile(uploaderUserId: currentUserId, fileName: "report.pdf", contentType: "application/pdf");

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId)));

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<UploadedFileId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([attachment]);

        Message? persistedMessage = null;
        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello dm", [attachment.Id]),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Attachments.Should().ContainSingle();
        response.Data.Attachments[0].FileId.Should().Be(attachment.Id.Value);
        persistedMessage.Should().NotBeNull();
        persistedMessage!.Attachments.Should().ContainSingle();
        persistedMessage.Attachments[0].FileId.Should().Be(attachment.Id);
    }

    [Fact]
    public async Task HandleAsync_WhenNotifierThrows_ShouldStillSucceed()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId)));

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessageCreatedAsync(
                It.IsAny<ConversationMessageCreatedNotification>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello"),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenContentContainsUrls_ShouldSucceed()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId)));

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _directMessageNotifierMock
            .Setup(x => x.NotifyMessagePreviewUpdatedAsync(
                It.IsAny<ConversationMessagePreviewUpdatedNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "Check https://example.com"),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenContentHasNoUrls_ShouldSucceedWithoutPreviewResolution()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId)));

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "Hello world, no links here"),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithValidReplyTarget_ShouldIncludeReplyToInResponseAndNotification()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());
        var targetMessageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId), CallerUsername: "sender", CallerDisplayName: "Sender Display"));

        _directMessageRepositoryMock
            .Setup(x => x.GetReplyTargetSummaryAsync(targetMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplyTargetSummary(
                targetMessageId,
                null,
                conversation.Id,
                UserId.New(),
                "targetuser",
                "Target Display",
                "target message content",
                false,
                false,
                null));

        Message? persistedMessage = null;
        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello", ReplyToMessageId: targetMessageId.Value),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.ReplyTo.Should().NotBeNull();
        response.Data.ReplyTo!.MessageId.Should().Be(targetMessageId.Value);
        response.Data.ReplyTo.AuthorUsername.Should().Be("targetuser");
        response.Data.ReplyTo.AuthorDisplayName.Should().Be("Target Display");
        response.Data.ReplyTo.Content.Should().Be("target message content");
        response.Data.ReplyTo.HasAttachments.Should().BeFalse();
        response.Data.ReplyTo.IsDeleted.Should().BeFalse();
        persistedMessage.Should().NotBeNull();
        persistedMessage!.ReplyToMessageId.Should().Be(targetMessageId);

        _directMessageNotifierMock.Verify(
            x => x.NotifyMessageCreatedAsync(
                It.Is<ConversationMessageCreatedNotification>(n =>
                    n.ReplyTo != null
                    && n.ReplyTo.MessageId == targetMessageId.Value
                    && n.ReplyTo.AuthorUsername == "targetuser"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithReplyTargetInDifferentConversation_ShouldReturnNotFound()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());
        var otherConversation = ApplicationTestBuilders.CreateConversation(UserId.New(), UserId.New());
        var targetMessageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId)));

        _directMessageRepositoryMock
            .Setup(x => x.GetReplyTargetSummaryAsync(targetMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplyTargetSummary(
                targetMessageId,
                null,
                otherConversation.Id,
                UserId.New(),
                "targetuser",
                null,
                "content",
                false,
                false,
                null));

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello", ReplyToMessageId: targetMessageId.Value),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentReplyTarget_ShouldReturnNotFound()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());
        var targetMessageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId)));

        _directMessageRepositoryMock
            .Setup(x => x.GetReplyTargetSummaryAsync(targetMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReplyTargetSummary?)null);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello", ReplyToMessageId: targetMessageId.Value),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithSoftDeletedReplyTarget_ShouldAcceptAndRenderDeletedShape()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());
        var targetMessageId = MessageId.New();
        var deletedAt = DateTime.UtcNow.AddHours(-1);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId)));

        _directMessageRepositoryMock
            .Setup(x => x.GetReplyTargetSummaryAsync(targetMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplyTargetSummary(
                targetMessageId,
                null,
                conversation.Id,
                UserId.New(),
                "deleteduser",
                "Deleted User",
                null,
                false,
                true,
                deletedAt));

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello", ReplyToMessageId: targetMessageId.Value),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.ReplyTo.Should().NotBeNull();
        response.Data.ReplyTo!.IsDeleted.Should().BeTrue();
        response.Data.ReplyTo.DeletedAtUtc.Should().Be(deletedAt);
        response.Data.ReplyTo.Content.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WithoutReply_ShouldHaveNullReplyTo()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, currentUserId)));

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hello"),
            currentUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.ReplyTo.Should().BeNull();
    }
}
