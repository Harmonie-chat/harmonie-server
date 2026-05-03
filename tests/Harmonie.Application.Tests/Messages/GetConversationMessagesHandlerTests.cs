using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
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
            UserId.New(),
            TestContext.Current.CancellationToken);

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
            userId,
            TestContext.Current.CancellationToken);

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
            .ReturnsAsync(new ConversationAccess(conversation, Participant: null));

        var response = await _handler.HandleAsync(
            new GetConversationMessagesInput(conversation.Id, Limit: 50),
            outsider,
            TestContext.Current.CancellationToken);

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
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

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
            participantOne,
            TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task HandleAsync_WhenMessagesHaveLinkPreviews_ShouldIncludeThem()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantTwo, content: "Check https://example.com", createdAtUtc: DateTime.UtcNow.AddMinutes(-1));

        var previews = new Dictionary<Guid, IReadOnlyList<LinkPreviewDto>>
        {
            [message.Id.Value] = [new LinkPreviewDto("https://example.com", "Title", "Desc", null, "Site")]
        };

        _directMessageRepositoryMock
            .Setup(x => x.GetConversationPageAsync(
                conversation.Id,
                It.IsAny<MessageCursor?>(),
                50,
                participantOne,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagePage(
                [message],
                null,
                new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>(),
                previews));

        var response = await _handler.HandleAsync(
            new GetConversationMessagesInput(conversation.Id, Limit: 50),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(1);
        response.Data.Items[0].LinkPreviews.Should().NotBeNull();
        response.Data.Items[0].LinkPreviews.Should().HaveCount(1);
        var firstPreview = response.Data.Items[0].LinkPreviews![0];
        firstPreview.Url.Should().Be("https://example.com");
        firstPreview.Title.Should().Be("Title");
    }

    [Fact]
    public async Task HandleAsync_WhenMessageHasNoReply_ShouldHaveNullReplyTo()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        var message = ApplicationTestBuilders.CreateConversationMessage(conversation.Id, participantOne, content: "no reply", createdAtUtc: DateTime.UtcNow.AddMinutes(-1));

        _directMessageRepositoryMock
            .Setup(x => x.GetConversationPageAsync(
                conversation.Id,
                It.IsAny<MessageCursor?>(),
                50,
                participantOne,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagePage(
                [message],
                null,
                new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>()));

        var response = await _handler.HandleAsync(
            new GetConversationMessagesInput(conversation.Id, Limit: 50),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(1);
        response.Data.Items[0].ReplyTo.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_WhenMessageHasReplyTo_ShouldIncludeReplyToFromPageData()
    {
        var participantOne = UserId.New();
        var participantTwo = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(participantOne, participantTwo);
        var targetMessageId = MessageId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdWithParticipantCheckAsync(conversation.Id, participantOne, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationAccess(conversation, Participant: ApplicationTestBuilders.CreateConversationParticipant(conversation.Id, participantOne)));

        var message = ApplicationTestBuilders.CreateConversationMessage(
            conversation.Id, participantOne, content: "replying",
            createdAtUtc: DateTime.UtcNow.AddMinutes(-1),
            replyToMessageId: targetMessageId);

        var replyPreviews = new Dictionary<Guid, ReplyPreviewDto>
        {
            [targetMessageId.Value] = new ReplyPreviewDto(
                targetMessageId.Value,
                Guid.NewGuid(),
                "Target Display",
                "targetuser",
                "target content",
                true,
                false,
                null)
        };

        _directMessageRepositoryMock
            .Setup(x => x.GetConversationPageAsync(
                conversation.Id,
                It.IsAny<MessageCursor?>(),
                50,
                participantOne,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagePage(
                [message],
                null,
                new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>(),
                ReplyPreviewsByTargetMessageId: replyPreviews));

        var response = await _handler.HandleAsync(
            new GetConversationMessagesInput(conversation.Id, Limit: 50),
            participantOne,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(1);
        var replyTo = response.Data.Items[0].ReplyTo!;
        replyTo.MessageId.Should().Be(targetMessageId.Value);
        replyTo.AuthorUsername.Should().Be("targetuser");
        replyTo.AuthorDisplayName.Should().Be("Target Display");
        replyTo.Content.Should().Be("target content");
        replyTo.HasAttachments.Should().BeTrue();
    }

}
