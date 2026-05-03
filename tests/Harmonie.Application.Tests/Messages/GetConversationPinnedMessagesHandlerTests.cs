using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Conversations.GetPinnedMessages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class GetConversationPinnedMessagesHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IPinnedMessageRepository> _pinnedMessageRepositoryMock;
    private readonly GetPinnedMessagesHandler _handler;

    public GetConversationPinnedMessagesHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _pinnedMessageRepositoryMock = new Mock<IPinnedMessageRepository>();

        _handler = new GetPinnedMessagesHandler(
            _conversationRepositoryMock.Object,
            _pinnedMessageRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnConversationNotFound()
    {
        var conversationId = ConversationId.New();
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversationId, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationAccess?)null);

        var response = await _handler.HandleAsync(new GetConversationPinnedMessagesInput(conversationId), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotParticipant_ShouldReturnConversationAccessDenied()
    {
        var conversation = ApplicationTestBuilders.CreateConversation(UserId.New(), UserId.New());
        var callerId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: null));

        var response = await _handler.HandleAsync(new GetConversationPinnedMessagesInput(conversation.Id), callerId, TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenNoPinnedMessages_ShouldReturnEmptyList()
    {
        var participant = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participant, UserId.New());
        var participantObj = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participant);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participantObj));

        var emptyPage = new PinnedMessagesPage(Array.Empty<PinnedMessageSummary>(), null);
        _pinnedMessageRepositoryMock
            .Setup(x => x.GetPinnedMessagesAsync(conversation.Id, participant, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyPage);

        var response = await _handler.HandleAsync(new GetConversationPinnedMessagesInput(conversation.Id), participant, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenPinnedMessagesExist_ShouldReturnList()
    {
        var participant = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participant, UserId.New());
        var participantObj = ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participant);
        var now = DateTime.UtcNow;

        var summaries = new[]
        {
            new PinnedMessageSummary(
                MessageId: Guid.NewGuid(), AuthorUserId: participant.Value,
                AuthorUsername: "dm_user", AuthorDisplayName: "DM User",
                Content: "pinned dm",
                Attachments: Array.Empty<MessageAttachmentDto>(),
                CreatedAtUtc: now, UpdatedAtUtc: null,
                PinnedByUserId: participant.Value, PinnedAtUtc: now)
        };

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, participantObj));

        var page = new PinnedMessagesPage(summaries, null);
        _pinnedMessageRepositoryMock
            .Setup(x => x.GetPinnedMessagesAsync(conversation.Id, participant, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var response = await _handler.HandleAsync(new GetConversationPinnedMessagesInput(conversation.Id), participant, TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.ConversationId.Should().Be(conversation.Id.Value);
        response.Data.Items.Should().HaveCount(1);
        response.Data.Items[0].Content.Should().Be("pinned dm");
    }
}
