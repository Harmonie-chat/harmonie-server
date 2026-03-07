using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.GetDirectMessages;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests;

public sealed class GetDirectMessagesHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IDirectMessageRepository> _directMessageRepositoryMock;
    private readonly GetDirectMessagesHandler _handler;

    public GetDirectMessagesHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _directMessageRepositoryMock = new Mock<IDirectMessageRepository>();

        _handler = new GetDirectMessagesHandler(
            _conversationRepositoryMock.Object,
            _directMessageRepositoryMock.Object,
            NullLogger<GetDirectMessagesHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenCursorIsInvalid_ShouldReturnValidationFailure()
    {
        var response = await _handler.HandleAsync(
            ConversationId.New(),
            new GetDirectMessagesRequest { Cursor = "invalid-cursor", Limit = 50 },
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
            .Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var response = await _handler.HandleAsync(
            conversationId,
            new GetDirectMessagesRequest { Limit = 50 },
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
        var conversation = CreateConversation(participantOne, participantTwo);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(
            conversation.Id,
            new GetDirectMessagesRequest { Limit = 50 },
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
        var conversation = CreateConversation(participantOne, participantTwo);
        var first = CreateDirectMessage(conversation.Id, participantOne, "First", DateTime.UtcNow.AddMinutes(-2));
        var second = CreateDirectMessage(conversation.Id, participantTwo, "Second", DateTime.UtcNow.AddMinutes(-1));
        var nextCursor = new DirectMessageCursor(first.CreatedAtUtc, first.Id);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.GetMessagesAsync(
                conversation.Id,
                It.IsAny<DirectMessageCursor?>(),
                50,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DirectMessagePage([second, first], nextCursor));

        var response = await _handler.HandleAsync(
            conversation.Id,
            new GetDirectMessagesRequest { Limit = 50 },
            participantOne);

        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Data.Should().NotBeNull();
        response.Data!.Items.Should().HaveCount(2);
        response.Data.Items[0].Content.Should().Be("First");
        response.Data.Items[1].Content.Should().Be("Second");
        response.Data.NextCursor.Should().NotBeNullOrEmpty();
    }

    private static Conversation CreateConversation(UserId user1Id, UserId user2Id)
    {
        var result = Conversation.Create(user1Id, user2Id);
        if (result.IsFailure || result.Value is null)
            throw new InvalidOperationException("Failed to create test conversation.");

        return result.Value;
    }

    private static DirectMessage CreateDirectMessage(
        ConversationId conversationId,
        UserId authorUserId,
        string content,
        DateTime createdAtUtc)
    {
        var contentResult = ChannelMessageContent.Create(content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Failed to create test direct message content.");

        return DirectMessage.Rehydrate(
            DirectMessageId.New(),
            conversationId,
            authorUserId,
            contentResult.Value,
            createdAtUtc,
            updatedAtUtc: null,
            deletedAtUtc: null);
    }
}
