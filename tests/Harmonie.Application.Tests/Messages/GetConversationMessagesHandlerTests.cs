using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.GetMessages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;


namespace Harmonie.Application.Tests.Messages;

public sealed class GetConversationMessagesHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessagePaginationRepository> _directMessageRepositoryMock;
    private readonly GetMessagesHandler _handler;

    public GetConversationMessagesHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _directMessageRepositoryMock = new Mock<IMessagePaginationRepository>();

        _handler = new GetMessagesHandler(
            _conversationRepositoryMock.Object,
            _directMessageRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenCursorIsInvalid_ShouldReturnValidationFailure()
    {
        var response = await _handler.HandleAsync(
            new GetConversationMessagesInput(ConversationId.New(), Cursor: "invalid-cursor", Limit: 50),
            UserId.New());

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
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
            new GetConversationMessagesInput(conversationId, Limit: 50),
            userId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
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
            .ReturnsAsync(new ConversationAccess(conversation, IsParticipant: false));

        var response = await _handler.HandleAsync(
            new GetConversationMessagesInput(conversation.Id, Limit: 50),
            outsider);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldReturnMessagesAscending()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var first = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne, content: "First", createdAtUtc: DateTime.UtcNow.AddMinutes(-2));
        var second = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantTwo, content: "Second", createdAtUtc: DateTime.UtcNow.AddMinutes(-1));
        var nextCursor = new MessageCursor(first.CreatedAtUtc, first.Id);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, IsParticipant: true));

        _directMessageRepositoryMock
            .Setup(x => x.GetConversationPageAsync(
                conversation.Id,
                It.IsAny<MessageCursor?>(),
                50,
                participantOne,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagePage(
                [second, first],
                nextCursor,
                new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>()));

        var response = await _handler.HandleAsync(
            new GetConversationMessagesInput(conversation.Id, Limit: 50),
            participantOne);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(2);
        response.Data.Items[0].Content.Should().Be("First");
        response.Data.Items[0].Attachments.Should().BeEmpty();
        response.Data.Items[0].Reactions.Should().BeEmpty();
        response.Data.Items[1].Content.Should().Be("Second");
        response.Data.NextCursor.Should().NotBeNullOrEmpty();
    }

}
