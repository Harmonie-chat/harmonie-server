using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class ReadOrchestratorTests
{
    public sealed record OrchestratorTestContext : ScopeContext;

    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUnitOfWorkTransaction> _transactionMock;
    private readonly ReadOrchestrator _orchestrator;

    private static readonly OrchestratorTestContext Ctx = new();

    public ReadOrchestratorTests()
    {
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = _unitOfWorkMock.SetupTransactionMock();

        _orchestrator = new ReadOrchestrator(
            _messageRepositoryMock.Object,
            _unitOfWorkMock.Object,
            TestTime.CreateProvider());
    }

    private static MessageScope ChannelScope() => new MessageScope.Channel(GuildChannelId.New());
    private static MessageId AnyMessageId() => MessageId.New();
    private static UserId AnyUser() => UserId.New();

    private static Mock<IReadScope<OrchestratorTestContext>> CreateAuthorizedScope(
        MessageId? latestMessageId = null)
    {
        var mock = new Mock<IReadScope<OrchestratorTestContext>>();
        mock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Authorized(Ctx));
        mock.Setup(x => x.GetLatestMessageIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestMessageId);
        mock.Setup(x => x.UpsertReadStateAsync(It.IsAny<MessageReadState>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    // ── Authorization ───────────────────────────────────────────────────

    [Fact]
    public async Task AcknowledgeAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scopeMock = new Mock<IReadScope<OrchestratorTestContext>>();
        scopeMock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Denied(
                new ApplicationError("AUTH_DENIED", "Not allowed")));

        var result = await _orchestrator.AcknowledgeAsync(
            scopeMock.Object, ChannelScope(), AnyMessageId(), AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    // ── Specific message provided ───────────────────────────────────────

    [Fact]
    public async Task AcknowledgeAsync_WhenSpecificMessageNotFound_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Message?)null);

        var result = await _orchestrator.AcknowledgeAsync(
            scope.Object, ChannelScope(), messageId, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task AcknowledgeAsync_WhenSpecificMessageWrongScope_ShouldReturnMessageNotFound()
    {
        var scope = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var wrongScope = new MessageScope.Channel(GuildChannelId.New());
        var message = ApplicationTestBuilders.CreateChannelMessage(
            ((MessageScope.Channel)wrongScope).ChannelId, AnyUser());
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var result = await _orchestrator.AcknowledgeAsync(
            scope.Object, ChannelScope(), messageId, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task AcknowledgeAsync_WhenSpecificMessageValid_ShouldUpsertAndCommit()
    {
        var scopeMock = CreateAuthorizedScope();
        var messageId = AnyMessageId();
        var messageScope = ChannelScope();
        var message = ApplicationTestBuilders.CreateChannelMessage(
            ((MessageScope.Channel)messageScope).ChannelId, AnyUser());
        _messageRepositoryMock
            .Setup(x => x.GetByIdAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        var result = await _orchestrator.AcknowledgeAsync(
            scopeMock.Object, messageScope, messageId, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        scopeMock.Verify(x => x.UpsertReadStateAsync(It.IsAny<MessageReadState>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── No specific message → fallback to latest ────────────────────────

    [Fact]
    public async Task AcknowledgeAsync_WhenNoLatestMessage_ShouldReturnOkEarly()
    {
        var scope = CreateAuthorizedScope(latestMessageId: null);

        var result = await _orchestrator.AcknowledgeAsync(
            scope.Object, ChannelScope(), null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        scope.Verify(x => x.UpsertReadStateAsync(It.IsAny<MessageReadState>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.BeginAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcknowledgeAsync_WhenFallsBackToLatest_ShouldUpsertAndCommit()
    {
        var latestId = AnyMessageId();
        var scopeMock = CreateAuthorizedScope(latestMessageId: latestId);

        var result = await _orchestrator.AcknowledgeAsync(
            scopeMock.Object, ChannelScope(), null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        scopeMock.Verify(x => x.GetLatestMessageIdAsync(It.IsAny<CancellationToken>()), Times.Once);
        scopeMock.Verify(x => x.UpsertReadStateAsync(It.IsAny<MessageReadState>(), It.IsAny<CancellationToken>()), Times.Once);
        _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
