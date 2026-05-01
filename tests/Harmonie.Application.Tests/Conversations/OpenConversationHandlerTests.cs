using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Conversations;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Conversations;
using Harmonie.Domain.Entities.Users;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Conversations;

public sealed class OpenConversationHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IConversationRepository> _conversationRepositoryMock;
    private readonly Mock<IConversationParticipantRepository> _participantRepositoryMock;
    private readonly OpenConversationHandler _handler;

    public OpenConversationHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _conversationRepositoryMock = new Mock<IConversationRepository>();
        _participantRepositoryMock = new Mock<IConversationParticipantRepository>();

        _participantRepositoryMock
            .Setup(x => x.GetByConversationIdAsync(It.IsAny<ConversationId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _handler = new OpenConversationHandler(
            _userRepositoryMock.Object,
            _conversationRepositoryMock.Object,
            _participantRepositoryMock.Object,
            new Mock<IRealtimeGroupManager>().Object,
            NullLogger<OpenConversationHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerTargetsSelf_ShouldReturnBadRequestError()
    {
        var callerUserId = UserId.New();

        var response = await _handler.HandleAsync(
            new OpenConversationRequest(callerUserId),
            callerUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.Conversation.CannotOpenSelf);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetUserDoesNotExist_ShouldReturnNotFound()
    {
        var callerUserId = UserId.New();
        var targetUserId = UserId.New();
        var callerUser = CreateUser(callerUserId, "caller");

        _userRepositoryMock
            .Setup(x => x.GetManyByIdsAsync(
                It.Is<IReadOnlyList<UserId>>(ids => ids.Contains(callerUserId) && ids.Contains(targetUserId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([callerUser]);

        var response = await _handler.HandleAsync(
            new OpenConversationRequest(targetUserId),
            callerUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationIsCreated_ShouldReturnCreatedPayload()
    {
        var callerUserId = UserId.New();
        var targetUserId = UserId.New();
        var callerUser = CreateUser(callerUserId, "caller");
        var targetUser = CreateUser(targetUserId, "target");
        var conversation = ApplicationTestBuilders.CreateConversation(callerUserId, targetUserId);

        _userRepositoryMock
            .Setup(x => x.GetManyByIdsAsync(
                It.Is<IReadOnlyList<UserId>>(ids => ids.Contains(callerUserId) && ids.Contains(targetUserId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([callerUser, targetUser]);

        _conversationRepositoryMock
            .Setup(x => x.GetOrCreateDirectAsync(callerUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationGetOrCreateResult(conversation, true));

        var response = await _handler.HandleAsync(
            new OpenConversationRequest(targetUserId),
            callerUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.ConversationId.Should().Be(conversation.Id.Value);
        response.Data.Created.Should().BeTrue();
        response.Data.Type.Should().Be("direct");
        response.Data.Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_WhenConversationAlreadyExists_ShouldReturnExistingPayload()
    {
        var callerUserId = UserId.New();
        var targetUserId = UserId.New();
        var callerUser = CreateUser(callerUserId, "caller");
        var targetUser = CreateUser(targetUserId, "target");
        var conversation = ApplicationTestBuilders.CreateConversation(callerUserId, targetUserId);

        _userRepositoryMock
            .Setup(x => x.GetManyByIdsAsync(
                It.Is<IReadOnlyList<UserId>>(ids => ids.Contains(callerUserId) && ids.Contains(targetUserId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([callerUser, targetUser]);

        _conversationRepositoryMock
            .Setup(x => x.GetOrCreateDirectAsync(callerUserId, targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationGetOrCreateResult(conversation, false));

        var response = await _handler.HandleAsync(
            new OpenConversationRequest(targetUserId),
            callerUserId,
            TestContext.Current.CancellationToken);

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Created.Should().BeFalse();
        response.Data.ConversationId.Should().Be(conversation.Id.Value);
    }

    private static User CreateUser(UserId userId, string suffix)
    {
        var emailResult = Email.Create($"{suffix}@harmonie.chat");
        var usernameResult = Username.Create($"user{suffix}");

        if (emailResult.IsFailure || emailResult.Value is null)
            throw new InvalidOperationException("Failed to create test email.");
        if (usernameResult.IsFailure || usernameResult.Value is null)
            throw new InvalidOperationException("Failed to create test username.");

        return User.Rehydrate(
            userId,
            emailResult.Value,
            usernameResult.Value,
            "hashed-password",
            avatarFileId: null,
            isEmailVerified: true,
            isActive: true,
            lastLoginAtUtc: null,
            displayName: null,
            bio: null,
            avatarColor: null,
            avatarIcon: null,
            avatarBg: null,
            theme: "default",
            language: null,
            status: "online",
            statusUpdatedAtUtc: null,
            createdAtUtc: DateTime.UtcNow,
            updatedAtUtc: DateTime.UtcNow);
    }
}
