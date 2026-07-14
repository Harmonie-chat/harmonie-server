using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.SendMessage;
using Harmonie.Application.Services;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Notifications;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Conversations;
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
    private readonly Mock<IConversationParticipantRepository> _participantRepositoryMock;
    private readonly Mock<IMessageRepository> _directMessageRepositoryMock;
    private readonly Mock<IMessageAttachmentRepository> _messageAttachmentRepositoryMock;
    private readonly Mock<IUploadedFileRepository> _uploadedFileRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<ILinkPreviewRepository> _linkPreviewRepositoryMock;
    private readonly Mock<ILinkPreviewFetcher> _linkPreviewFetcherMock;
    private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
    private readonly Mock<IMessageEventPublisher> _directMessageNotifierMock;
    private readonly Mock<IMessageNotificationOutboxRepository> _messageNotificationOutboxRepositoryMock;
    private readonly MessageSendOrchestrator _orchestrator;
    private readonly SendMessageHandler _handler;

    public SendConversationMessageHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _participantRepositoryMock = new Mock<IConversationParticipantRepository>();
        _directMessageRepositoryMock = new Mock<IMessageRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _uploadedFileRepositoryMock = new Mock<IUploadedFileRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _linkPreviewRepositoryMock = new Mock<ILinkPreviewRepository>();
        _linkPreviewFetcherMock = new Mock<ILinkPreviewFetcher>();
        _directMessageNotifierMock = new Mock<IMessageEventPublisher>();
        _messageNotificationOutboxRepositoryMock = new Mock<IMessageNotificationOutboxRepository>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _directMessageNotifierMock
            .Setup(x => x.PublishCreatedAsync(
                It.IsAny<MessageCreatedEventEnvelope>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _messageNotificationOutboxRepositoryMock
            .Setup(x => x.AddPendingAsync(
                It.IsAny<MessageId>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeMock = new Mock<IServiceScope>();
        var scopeProviderMock = new Mock<IServiceProvider>();
        scopeProviderMock.Setup(s => s.GetService(typeof(ILinkPreviewRepository)))
            .Returns(_linkPreviewRepositoryMock.Object);
        scopeProviderMock.Setup(s => s.GetService(typeof(ILinkPreviewFetcher)))
            .Returns(_linkPreviewFetcherMock.Object);
        scopeProviderMock.Setup(s => s.GetService(typeof(IMessageEventPublisher)))
            .Returns(_directMessageNotifierMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(scopeProviderMock.Object);

        _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeFactoryMock.Setup(f => f.CreateScope())
            .Returns(scopeMock.Object);

        _messageAttachmentRepositoryMock = new Mock<IMessageAttachmentRepository>();

        _orchestrator = new MessageSendOrchestrator(
            _directMessageRepositoryMock.Object,
            _messageAttachmentRepositoryMock.Object,
            new MessageAttachmentResolver(_uploadedFileRepositoryMock.Object),
            _userRepositoryMock.Object,
            _messageNotificationOutboxRepositoryMock.Object,
            _unitOfWorkMock.Object,
            TestTime.CreateProvider());

        _handler = new SendMessageHandler(
            _conversationRepositoryMock.Object,
            _participantRepositoryMock.Object,
            _directMessageNotifierMock.Object,
            new LinkPreviewResolutionService(
                _serviceScopeFactoryMock.Object,
                TestTime.CreateProvider(),
                NullLogger<LinkPreviewResolutionService>.Instance),
            NullLogger<ConversationSendMessageScope>.Instance,
            _orchestrator);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var conversationId = ConversationId.New();
        var userId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversationId, It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccessWithAllParticipants?)null);

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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, outsider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(conversation, CallerParticipant: null, AllParticipants: [], null, null));

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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId));

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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId, callerUsername: "sender", callerDisplayName: "Sender Display"));

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
            x => x.PublishCreatedAsync(
                It.Is<MessageCreatedEventEnvelope>(n =>
                    n.ConversationId == conversation.Id
                    && n.ConversationName == null
                    && n.ConversationType == "Direct"
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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId));

        _uploadedFileRepositoryMock
            .Setup(x => x.GetByIdsAsync(It.IsAny<IReadOnlyCollection<UploadedFileId>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([attachment]);

        Message? persistedMessage = null;
        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Callback<Message, CancellationToken>((message, _) => persistedMessage = message)
            .Returns(Task.CompletedTask);

        IReadOnlyCollection<MessageAttachment>? persistedAttachments = null;
        _messageAttachmentRepositoryMock
            .Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<MessageAttachment>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<MessageAttachment>, CancellationToken>((items, _) => persistedAttachments = items.ToArray())
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
        persistedAttachments.Should().NotBeNull();
        persistedAttachments!.Should().ContainSingle();
        persistedAttachments!.First().FileId.Should().Be(attachment.Id);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageEventPublisherSucceeds_ShouldStillSucceed()
    {
        var currentUserId = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(currentUserId, UserId.New());

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId));

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _directMessageNotifierMock
            .Setup(x => x.PublishCreatedAsync(
                It.IsAny<MessageCreatedEventEnvelope>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId));

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _directMessageNotifierMock
            .Setup(x => x.PublishPreviewUpdatedAsync(
                It.IsAny<MessagePreviewUpdatedEventEnvelope>(),
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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId));

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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId, callerUsername: "sender", callerDisplayName: "Sender Display"));

        _directMessageRepositoryMock
            .Setup(x => x.GetReplyTargetSummaryAsync(targetMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplyTargetSummary(
                targetMessageId,
                new MessageScope.Conversation(conversation.Id),
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
            x => x.PublishCreatedAsync(
                It.Is<MessageCreatedEventEnvelope>(n =>
                    n.ReplyTo != null
                    && n.ReplyTo.MessageId == targetMessageId.Value
                    && n.ReplyTo.AuthorUsername == "targetuser"
                    && n.ConversationName == null
                    && n.ConversationType == "Direct"),
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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId));

        _directMessageRepositoryMock
            .Setup(x => x.GetReplyTargetSummaryAsync(targetMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplyTargetSummary(
                targetMessageId,
                new MessageScope.Conversation(otherConversation.Id),
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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId));

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
        var deletedAt = TestTime.UtcNow.AddHours(-1);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId));

        _directMessageRepositoryMock
            .Setup(x => x.GetReplyTargetSummaryAsync(targetMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplyTargetSummary(
                targetMessageId,
                new MessageScope.Conversation(conversation.Id),
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
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildAccess(conversation, currentUserId));

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

    [Fact]
    public async Task HandleAsync_WhenDirectConversationHasHiddenParticipant_ShouldUnhideAndPersist()
    {
        var sender = UserId.New();
        var receiver = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(sender, receiver);

        var senderParticipant = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, sender);
        var hiddenParticipant = ConversationParticipant.Rehydrate(
            conversation.Id, receiver, TestTime.UtcNow.AddDays(-1), hiddenAtUtc: TestTime.UtcNow.AddHours(-1));

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, sender, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(
                conversation,
                CallerParticipant: senderParticipant,
                AllParticipants: [senderParticipant, hiddenParticipant],
                CallerUsername: "sender",
                CallerDisplayName: null));

        _directMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new SendConversationMessageInput(conversation.Id, "hey"),
            sender,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        hiddenParticipant.HiddenAtUtc.Should().BeNull();
        _participantRepositoryMock.Verify(
            x => x.UpdateRangeAsync(
                It.Is<IReadOnlyList<ConversationParticipant>>(list => list.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ConversationAccessWithAllParticipants BuildAccess(
        Conversation conversation,
        UserId callerId,
        string? callerUsername = null,
        string? callerDisplayName = null)
    {
        var callerParticipant = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, callerId);
        return new ConversationAccessWithAllParticipants(
            conversation,
            CallerParticipant: callerParticipant,
            AllParticipants: [callerParticipant],
            callerUsername,
            callerDisplayName);
    }
}
