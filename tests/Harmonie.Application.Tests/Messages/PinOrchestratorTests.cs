using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.Pins;
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

public sealed class PinOrchestratorTests
{
    public sealed record OrchestratorTestContext : ScopeContext;

    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IPinnedMessageRepository> _pinnedMessageRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly PinOrchestrator _orchestrator;

    private static readonly OrchestratorTestContext Ctx = new();

    public PinOrchestratorTests()
    {
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _pinnedMessageRepositoryMock = new Mock<IPinnedMessageRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _orchestrator = new PinOrchestrator(
            _messageRepositoryMock.Object,
            _pinnedMessageRepositoryMock.Object,
            _unitOfWorkMock.Object,
            TestTime.CreateProvider());
    }

    private static MessageScope ChannelScope() => new MessageScope.Channel(GuildChannelId.New());
    private static MessageId AnyMessageId() => MessageId.New();
    private static UserId AnyUser() => UserId.New();

    private static Mock<IPinScope<OrchestratorTestContext>> CreateAuthorizedScope()
    {
        var mock = new Mock<IPinScope<OrchestratorTestContext>>();
        mock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Authorized(Ctx));
        mock.Setup(x => x.NotifyPinAddedAsync(
            Ctx, It.IsAny<MessageId>(), It.IsAny<UserId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.NotifyPinRemovedAsync(
            Ctx, It.IsAny<MessageId>(), It.IsAny<UserId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private void SetupMessageExists(MessageId messageId, MessageScope scope)
    {
        var message = scope is MessageScope.Channel c
            ? ApplicationTestBuilders.CreateChannelMessage(c.ChannelId, AnyUser())
            : ApplicationTestBuilders.CreateConversationMessage(
                ((MessageScope.Conversation)scope).ConversationId, AnyUser());
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
    }

    // ── PinAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task PinAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scopeMock = new Mock<IPinScope<OrchestratorTestContext>>();
        scopeMock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Denied(
                new ApplicationError("AUTH_DENIED", "Not allowed")));

        var result = await _orchestrator.PinAsync(
            scopeMock.Object, ChannelScope(), AnyMessageId(), AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    [Fact]
    public async Task PinAsync_WhenMessageNotFound_ShouldReturnPinMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var result = await _orchestrator.PinAsync(
            scope.Object, ChannelScope(), messageId, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Pin.MessageNotFound);
    }

    [Fact]
    public async Task PinAsync_WhenSuccessful_ShouldPersistCommitAndNotify()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        SetupMessageExists(messageId, messageScope);

        var callOrder = new List<string>();
        _pinnedMessageRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<PinnedMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("add"))
            .Returns(Task.CompletedTask);
        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);
        scopeMock
            .Setup(x => x.NotifyPinAddedAsync(
                Ctx, messageId, It.IsAny<UserId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("notify"))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.PinAsync(
            scopeMock.Object, messageScope, messageId, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _pinnedMessageRepositoryMock.Verify(x => x.AddAsync(It.IsAny<PinnedMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        scopeMock.Verify(
            x => x.NotifyPinAddedAsync(Ctx, messageId, It.IsAny<UserId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        callOrder.Should().Equal("add", "commit", "notify");
    }

    // ── UnpinAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UnpinAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scopeMock = new Mock<IPinScope<OrchestratorTestContext>>();
        scopeMock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Denied(
                new ApplicationError("AUTH_DENIED", "Not allowed")));

        var result = await _orchestrator.UnpinAsync(
            scopeMock.Object, ChannelScope(), AnyMessageId(), AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    [Fact]
    public async Task UnpinAsync_WhenMessageNotFound_ShouldReturnPinMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var result = await _orchestrator.UnpinAsync(
            scope.Object, ChannelScope(), messageId, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Pin.MessageNotFound);
    }

    [Fact]
    public async Task UnpinAsync_WhenSuccessful_ShouldRemoveCommitAndNotify()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        SetupMessageExists(messageId, messageScope);

        var callOrder = new List<string>();
        _pinnedMessageRepositoryMock
            .Setup(x => x.RemoveAsync(messageId, It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("remove"))
            .Returns(Task.CompletedTask);
        _transactionMock
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);
        scopeMock
            .Setup(x => x.NotifyPinRemovedAsync(
                Ctx, messageId, It.IsAny<UserId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("notify"))
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.UnpinAsync(
            scopeMock.Object, messageScope, messageId, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _pinnedMessageRepositoryMock.Verify(x => x.RemoveAsync(messageId, It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        scopeMock.Verify(
            x => x.NotifyPinRemovedAsync(Ctx, messageId, It.IsAny<UserId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        callOrder.Should().Equal("remove", "commit", "notify");
    }
}
