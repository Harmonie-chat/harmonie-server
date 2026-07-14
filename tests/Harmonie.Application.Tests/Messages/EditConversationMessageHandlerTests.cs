using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Common.Uploads;
using Harmonie.Application.Features.Conversations.EditMessage;
using Harmonie.Application.Features.Conversations.Messages;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Interfaces.Uploads;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class EditConversationMessageHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageRepository> _directMessageRepositoryMock;
    private readonly Mock<IMessageAttachmentRepository> _messageAttachmentRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly Mock<IMessageEventPublisher> _directMessageNotifierMock;
    private readonly MessageEditDeleteOrchestrator _orchestrator;
    private readonly EditMessageHandler _handler;

    public EditConversationMessageHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _directMessageRepositoryMock = new Mock<IMessageRepository>();
        _messageAttachmentRepositoryMock = new Mock<IMessageAttachmentRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<IUnitOfWorkTransaction>();
        _directMessageNotifierMock = new Mock<IMessageEventPublisher>();

        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _directMessageNotifierMock
            .Setup(x => x.PublishUpdatedAsync(It.IsAny<MessageUpdatedEventEnvelope>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _messageAttachmentRepositoryMock
            .Setup(x => x.GetByMessageIdAsync(It.IsAny<MessageId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MessageAttachment>());

        _directMessageRepositoryMock
            .Setup(x => x.GetMentionedUserIdsByMessageIdAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<Guid>>());

        var uploadedFileCleanupService = new UploadedFileCleanupService(
            new Mock<IUploadedFileRepository>().Object,
            new Mock<IObjectStorageService>().Object,
            NullLogger<UploadedFileCleanupService>.Instance);

        _orchestrator = new MessageEditDeleteOrchestrator(
            _directMessageRepositoryMock.Object,
            _messageAttachmentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _unitOfWorkMock.Object,
            uploadedFileCleanupService,
            TestClock.Create());

        _handler = new EditMessageHandler(
            _conversationRepositoryMock.Object,
            _directMessageNotifierMock.Object,
            NullLogger<ConversationMessageEditDeleteScope>.Instance,
            _orchestrator);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_ShouldReturnMessageContentEmpty()
    {
        var response = await _handler.HandleAsync(
            new EditConversationMessageInput(ConversationId.New(), MessageId.New(), "   "),
            UserId.New(),
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnConversationNotFound()
    {
        var conversationId = ConversationId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversationId, It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccessWithAllParticipants?)null);

        var response = await _handler.HandleAsync(
            new EditConversationMessageInput(conversationId, MessageId.New(), "updated content"),
            callerId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotParticipant_ShouldReturnConversationAccessDenied()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var outsider = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, outsider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(conversation, CallerParticipant: null, AllParticipants: Array.Empty<ConversationParticipant>(), CallerUsername: null, CallerDisplayName: null));

        var response = await _handler.HandleAsync(
            new EditConversationMessageInput(conversation.Id, MessageId.New(), "updated content"),
            outsider,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageDoesNotExist_ShouldReturnMessageNotFound()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(conversation, CallerParticipant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne), AllParticipants: Array.Empty<ConversationParticipant>(), CallerUsername: null, CallerDisplayName: null));

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var response = await _handler.HandleAsync(
            new EditConversationMessageInput(conversation.Id, messageId, "updated content"),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageBelongsToAnotherConversation_ShouldReturnMessageNotFound()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var messageFromOtherConversation = ApplicationTestBuilders.CreateConversationMessage(ConversationId.New(), participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(conversation, CallerParticipant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne), AllParticipants: Array.Empty<ConversationParticipant>(), CallerUsername: null, CallerDisplayName: null));

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageFromOtherConversation);

        var response = await _handler.HandleAsync(
            new EditConversationMessageInput(conversation.Id, messageId, "updated content"),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotAuthor_ShouldReturnEditForbidden()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(conversation, CallerParticipant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne), AllParticipants: Array.Empty<ConversationParticipant>(), CallerUsername: null, CallerDisplayName: null));

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            new EditConversationMessageInput(conversation.Id, messageId, "updated content"),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Message.EditForbidden);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldReturnUpdatedMessage()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(conversation, CallerParticipant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne), AllParticipants: Array.Empty<ConversationParticipant>(), CallerUsername: null, CallerDisplayName: null));

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            new EditConversationMessageInput(conversation.Id, messageId, "  updated content  "),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Content.Should().Be("updated content");
        response.Data.ConversationId.Should().Be(conversation.Id.Value);
        response.Data.AuthorUserId.Should().Be(participantOne.Value);
        response.Data.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenAuthorEditsOwnMessage_ShouldPersistCommitAndNotify()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(conversation, CallerParticipant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne), AllParticipants: Array.Empty<ConversationParticipant>(), CallerUsername: null, CallerDisplayName: null));

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var response = await _handler.HandleAsync(
            new EditConversationMessageInput(conversation.Id, messageId, "updated content"),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        _directMessageRepositoryMock.Verify(
            x => x.UpdateAsync(
                It.Is<Message>(m =>
                    m.Id == message.Id
                    && m.Content!.Value == "updated content"
                    && m.UpdatedAtUtc != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _directMessageNotifierMock.Verify(
            x => x.PublishUpdatedAsync(
                It.Is<MessageUpdatedEventEnvelope>(n =>
                    n.MessageId == message.Id
                    && n.ConversationId == conversation.Id
                    && n.ConversationName == null
                    && n.ConversationType == "Direct"
                    && n.Content == "updated content"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageEventPublisherSucceeds_ShouldStillSucceed()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var messageId = MessageId.New();
        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithAllParticipantsAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccessWithAllParticipants(conversation, CallerParticipant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne), AllParticipants: Array.Empty<ConversationParticipant>(), CallerUsername: null, CallerDisplayName: null));

        _directMessageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _directMessageNotifierMock
            .Setup(x => x.PublishUpdatedAsync(
                It.IsAny<MessageUpdatedEventEnvelope>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new EditConversationMessageInput(conversation.Id, messageId, "updated content"),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
