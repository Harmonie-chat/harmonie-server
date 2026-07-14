using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class MessageFetchOrchestratorTests
{
    public sealed record OrchestratorTestContext : ScopeContext;

    private readonly MessageFetchOrchestrator _orchestrator;

    public MessageFetchOrchestratorTests()
    {
        _orchestrator = new MessageFetchOrchestrator();
    }

    private static MessageScope ChannelScope() => new MessageScope.Channel(GuildChannelId.New());
    private static UserId AnyUser() => UserId.New();

    private static Mock<IMessagePageScope<OrchestratorTestContext>> CreateAuthorizedScope(MessagePage? page = null)
    {
        var mock = new Mock<IMessagePageScope<OrchestratorTestContext>>();
        mock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Authorized(
                new OrchestratorTestContext()));
        mock.Setup(x => x.GetPageAsync(It.IsAny<MessageCursor?>(), It.IsAny<int>(), It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page ?? CreateEmptyPage());
        return mock;
    }

    private static MessagePage CreateEmptyPage() => new(
        Items: Array.Empty<Message>(),
        NextCursor: null,
        ReactionsByMessageId: new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>(),
        AttachmentsByMessageId: new Dictionary<Guid, IReadOnlyList<MessageAttachment>>());

    // ── Cursor validation ───────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_WhenCursorInvalid_ShouldReturnValidationFailed()
    {
        var scope = CreateAuthorizedScope();

        var result = await _orchestrator.FetchAsync(
            scope.Object, "not-a-valid-cursor", null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    // ── Authorization ───────────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_WhenAuthorizationDenied_ShouldReturnAuthError()
    {
        var scopeMock = new Mock<IMessagePageScope<OrchestratorTestContext>>();
        scopeMock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Denied(
                new ApplicationError("AUTH_DENIED", "Not allowed")));

        var result = await _orchestrator.FetchAsync(
            scopeMock.Object, null, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_DENIED");
    }

    // ── Successful fetch ────────────────────────────────────────────────

    [Fact]
    public async Task FetchAsync_WhenSuccessful_ShouldReturnMappedItems()
    {
        var scope = CreateAuthorizedScope();

        var result = await _orchestrator.FetchAsync(
            scope.Object, null, 10, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_ShouldClampLimit()
    {
        var scopeMock = CreateAuthorizedScope();

        await _orchestrator.FetchAsync(
            scopeMock.Object, null, null, AnyUser(), TestContext.Current.CancellationToken);

        // Default limit is 50, so GetPageAsync should be called with limit=50
        scopeMock.Verify(
            x => x.GetPageAsync(null, 50, It.IsAny<UserId>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchAsync_ShouldEncodeNextCursor()
    {
        var channelId = GuildChannelId.New();
        var message = ApplicationTestBuilders.CreateChannelMessage(channelId, UserId.New(), "hello");
        var cursor = new MessageCursor(TestClock.UtcNow, message.Id);
        var page = new MessagePage(
            Items: [message],
            NextCursor: cursor,
            ReactionsByMessageId: new Dictionary<Guid, IReadOnlyList<MessageReactionSummary>>(),
            AttachmentsByMessageId: new Dictionary<Guid, IReadOnlyList<MessageAttachment>>());

        var scopeMock = CreateAuthorizedScope(page);

        var result = await _orchestrator.FetchAsync(
            scopeMock.Object, null, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        result.Data.NextCursor.Should().NotBeNull();
    }
}
