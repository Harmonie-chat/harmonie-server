using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Tests.Common;
using Harmonie.Application.Features.Conversations.SearchConversationMessages;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;


namespace Harmonie.Application.Tests.Messages;

public sealed class SearchConversationMessagesHandlerTests
{
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IMessageSearchRepository> _directMessageRepositoryMock;
    private readonly SearchConversationMessagesHandler _handler;

    public SearchConversationMessagesHandlerTests()
    {
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _directMessageRepositoryMock = new Mock<IMessageSearchRepository>();

        _handler = new SearchConversationMessagesHandler(
            _conversationRepositoryMock.Object,
            _directMessageRepositoryMock.Object,
            NullLogger<SearchConversationMessagesHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var conversationId = ConversationId.New();
        var currentUserId = UserId.New();

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        var response = await _handler.HandleAsync(
            conversationId,
            new SearchConversationMessagesRequest { Q = "deploy" },
            currentUserId);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotParticipant_ShouldReturnAccessDenied()
    {
        var user1 = UserId.New();
        var user2 = UserId.New();
        var outsider = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(user1, user2);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SearchConversationMessagesRequest { Q = "deploy" },
            outsider);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WithValidRequest_ShouldReturnMappedItemsAndCursor()
    {
        var user1 = UserId.New();
        var user2 = UserId.New();
        var conversation = ApplicationTestBuilders.CreateConversation(user1, user2);
        var before = new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc);
        var after = before.AddHours(-2);
        var item = CreateSearchItem(
            authorUserId: user2,
            content: "deploy succeeded",
            createdAtUtc: after.AddMinutes(30));
        var nextCursor = new MessageCursor(item.CreatedAtUtc, item.MessageId);

        _conversationRepositoryMock
            .Setup(x => x.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _directMessageRepositoryMock
            .Setup(x => x.SearchConversationMessagesAsync(
                It.Is<SearchConversationMessagesQuery>(query =>
                    query.ConversationId == conversation.Id
                    && query.SearchText == "deploy"
                    && query.BeforeCreatedAtUtc == before
                    && query.AfterCreatedAtUtc == after),
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchConversationMessagesPage([item], nextCursor));

        var response = await _handler.HandleAsync(
            conversation.Id,
            new SearchConversationMessagesRequest
            {
                Q = " deploy ",
                Before = before.ToString("O"),
                After = after.ToString("O"),
                Limit = 10
            },
            user1);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.ConversationId.Should().Be(conversation.Id.ToString());
        response.Data.Items.Should().ContainSingle();
        response.Data.Items[0].AuthorUserId.Should().Be(user2.ToString());
        response.Data.Items[0].AuthorUsername.Should().Be("participant-two");
        response.Data.Items[0].Content.Should().Be("deploy succeeded");
        response.Data.Items[0].Attachments.Should().BeEmpty();
        response.Data.NextCursor.Should().NotBeNullOrWhiteSpace();
    }

    private static SearchConversationMessagesItem CreateSearchItem(
        UserId authorUserId,
        string content,
        DateTime createdAtUtc)
    {
        var contentResult = MessageContent.Create(content);
        if (contentResult.IsFailure || contentResult.Value is null)
            throw new InvalidOperationException("Failed to create test conversation message content.");

        return new SearchConversationMessagesItem(
            MessageId: MessageId.New(),
            AuthorUserId: authorUserId,
            AuthorUsername: "participant-two",
            AuthorDisplayName: "Participant Two",
            AuthorAvatarFileId: UploadedFileId.From(Guid.Parse("3d3eb034-f75d-4a59-9f59-2f84ebf6e58c")),
            AuthorAvatarColor: null,
            AuthorAvatarIcon: null,
            AuthorAvatarBg: null,
            Attachments: Array.Empty<MessageAttachment>(),
            Content: contentResult.Value,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: null);
    }
}
