using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.Reactions;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class ReactionOrchestratorTests
{
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IMessageReactionRepository> _reactionRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly ReactionOrchestrator _orchestrator;

    private static readonly ChannelReactionScope.Context Ctx = new(
        GuildChannelId.New(), "general", GuildId.New(), "MyGuild", "caller", "Display");

    public ReactionOrchestratorTests()
    {
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _reactionRepositoryMock = new Mock<IMessageReactionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _orchestrator = new ReactionOrchestrator(
            _messageRepositoryMock.Object,
            _reactionRepositoryMock.Object,
            _unitOfWorkMock.Object,
            TestClock.Create());
    }

    private static MessageScope AnyScope() => new MessageScope.Channel(GuildChannelId.New());
    private static MessageId AnyMessageId() => MessageId.New();
    private static UserId AnyUser() => UserId.New();
    private const string TestEmoji = "👍";

    private static Mock<IReactionScope<ChannelReactionScope.Context>> CreateAuthorizedScope()
    {
        var mock = new Mock<IReactionScope<ChannelReactionScope.Context>>();
        mock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<ChannelReactionScope.Context>.Authorized(Ctx));
        mock.Setup(x => x.NotifyReactionAddedAsync(
            Ctx, It.IsAny<MessageId>(), It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.NotifyReactionRemovedAsync(
            Ctx, It.IsAny<MessageId>(), It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private void SetupMessageExists(MessageId messageId, MessageScope scope)
    {
        Message message;
        if (scope is MessageScope.Channel c)
            message = ApplicationTestBuilders.CreateChannelMessage(c.ChannelId, AnyUser());
        else
            message = ApplicationTestBuilders.CreateConversationMessage(
                ((MessageScope.Conversation)scope).ConversationId, AnyUser());
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
    }

    // ── AddAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scopeMock = new Mock<IReactionScope<ChannelReactionScope.Context>>();
        scopeMock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<ChannelReactionScope.Context>.Denied(
                new ApplicationError("AUTH_DENIED", "Not allowed")));

        var result = await _orchestrator.AddAsync(
            scopeMock.Object, AnyScope(), AnyMessageId(), TestEmoji, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    [Fact]
    public async Task AddAsync_WhenMessageNotFound_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var result = await _orchestrator.AddAsync(
            scope.Object, AnyScope(), messageId, TestEmoji, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    [Fact]
    public async Task AddAsync_WhenMessageInWrongScope_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var wrongScope = new MessageScope.Conversation(ConversationId.New());
        SetupMessageExists(messageId, wrongScope);

        var result = await _orchestrator.AddAsync(
            scope.Object, AnyScope(), messageId, TestEmoji, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    [Fact]
    public async Task AddAsync_WhenSuccessful_ShouldCreateReactionAndNotify()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var scope = AnyScope();
        SetupMessageExists(messageId, scope);
        var callOrder = new List<string>();

        _reactionRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<MessageReaction>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("add"))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);

        scopeMock
            .Setup(x => x.NotifyReactionAddedAsync(
                Ctx, messageId, It.IsAny<UserId>(), TestEmoji, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("notify"))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.AddAsync(
            scopeMock.Object, scope, messageId, TestEmoji, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _reactionRepositoryMock.Verify(x => x.AddAsync(It.IsAny<MessageReaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        callOrder.Should().Equal("add", "commit", "notify");
    }

    // ── RemoveAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scopeMock = new Mock<IReactionScope<ChannelReactionScope.Context>>();
        scopeMock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<ChannelReactionScope.Context>.Denied(
                new ApplicationError("AUTH_DENIED", "Not allowed")));

        var result = await _orchestrator.RemoveAsync(
            scopeMock.Object, AnyScope(), AnyMessageId(), TestEmoji, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    [Fact]
    public async Task RemoveAsync_WhenMessageNotFound_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var result = await _orchestrator.RemoveAsync(
            scope.Object, AnyScope(), messageId, TestEmoji, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    [Fact]
    public async Task RemoveAsync_WhenSuccessful_ShouldRemoveReactionAndNotify()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var scope = AnyScope();
        SetupMessageExists(messageId, scope);
        var callOrder = new List<string>();

        _reactionRepositoryMock
            .Setup(x => x.RemoveAsync(messageId, It.IsAny<UserId>(), TestEmoji, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("remove"))
            .Returns(Task.CompletedTask);

        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);

        scopeMock
            .Setup(x => x.NotifyReactionRemovedAsync(
                Ctx, messageId, It.IsAny<UserId>(), TestEmoji, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("notify"))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.RemoveAsync(
            scopeMock.Object, scope, messageId, TestEmoji, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _reactionRepositoryMock.Verify(
            x => x.RemoveAsync(messageId, It.IsAny<UserId>(), TestEmoji, It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        callOrder.Should().Equal("remove", "commit", "notify");
    }

    // ── GetUsersAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetUsersAsync_WhenAuthorizationDenied_ShouldReturnAuthError_BeforeCursorValidation()
    {
        var scopeMock = new Mock<IReactionScope<ChannelReactionScope.Context>>();
        scopeMock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<ChannelReactionScope.Context>.Denied(
                new ApplicationError("AUTH_DENIED", "Not allowed")));

        var result = await _orchestrator.GetUsersAsync(
            scopeMock.Object, AnyScope(), AnyMessageId(), TestEmoji,
            "invalid-cursor", 50, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    [Fact]
    public async Task GetUsersAsync_WhenCursorInvalid_ShouldReturnValidationFailed()
    {
        var scope = CreateAuthorizedScope();

        var result = await _orchestrator.GetUsersAsync(
            scope.Object, AnyScope(), AnyMessageId(), TestEmoji,
            "not-a-valid-cursor", 50, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task GetUsersAsync_WhenMessageNotFound_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var result = await _orchestrator.GetUsersAsync(
            scope.Object, AnyScope(), messageId, TestEmoji,
            null, 50, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    [Fact]
    public async Task GetUsersAsync_WhenSuccessful_ShouldClampLimitAndEncodeNextCursor()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = AnyScope();
        SetupMessageExists(messageId, messageScope);

        _reactionRepositoryMock
            .Setup(x => x.GetReactionUsersAsync(messageId, TestEmoji, 50, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReactionUsersPage(
                Users: [new ReactionUser(Guid.NewGuid(), "user1", null)],
                TotalCount: 1,
                NextCursor: null));

        var result = await _orchestrator.GetUsersAsync(
            scope.Object, messageScope, messageId, TestEmoji,
            null, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data!.Users.Should().HaveCount(1);
        result.Data.TotalCount.Should().Be(1);
    }
}
