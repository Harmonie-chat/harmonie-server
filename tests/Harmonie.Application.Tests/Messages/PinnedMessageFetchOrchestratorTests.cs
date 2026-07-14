using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Application.Tests.Common;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Moq;
using Xunit;

namespace Harmonie.Application.Tests.Messages;

public sealed class PinnedMessageFetchOrchestratorTests
{
    public sealed record OrchestratorTestContext : ScopeContext;

    private readonly PinnedMessageFetchOrchestrator _orchestrator;

    public PinnedMessageFetchOrchestratorTests()
    {
        _orchestrator = new PinnedMessageFetchOrchestrator();
    }

    private static UserId AnyUser() => UserId.New();

    private static Mock<IPinnedMessageFetchScope<OrchestratorTestContext>> CreateAuthorizedScope(
        PinnedMessagesPage? page = null)
    {
        var mock = new Mock<IPinnedMessageFetchScope<OrchestratorTestContext>>();
        mock.Setup(x => x.AuthorizeAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationResult<OrchestratorTestContext>.Authorized(
                new OrchestratorTestContext()));
        mock.Setup(x => x.GetPinnedPageAsync(
                It.IsAny<UserId>(), It.IsAny<PinnedMessagesCursor?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page ?? new PinnedMessagesPage(
                Items: Array.Empty<PinnedMessageSummary>(),
                NextCursor: null,
                AttachmentsByMessageId: new Dictionary<MessageId, IReadOnlyList<MessageAttachment>>()));
        return mock;
    }

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
        var scopeMock = new Mock<IPinnedMessageFetchScope<OrchestratorTestContext>>();
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

        scopeMock.Verify(
            x => x.GetPinnedPageAsync(It.IsAny<UserId>(), null, 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchAsync_ShouldEncodeNextCursor()
    {
        var cursor = new PinnedMessagesCursor(TestTime.UtcNow, Guid.NewGuid());
        var page = new PinnedMessagesPage(
            Items: Array.Empty<PinnedMessageSummary>(),
            NextCursor: cursor,
            AttachmentsByMessageId: new Dictionary<MessageId, IReadOnlyList<MessageAttachment>>());

        var scopeMock = CreateAuthorizedScope(page);

        var result = await _orchestrator.FetchAsync(
            scopeMock.Object, null, null, AnyUser(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Data!.NextCursor.Should().NotBeNull();
    }
}
