using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.ListConversations;
using Xunit;

namespace Harmonie.API.IntegrationTests.Conversations;

public sealed class DeleteConversationTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DeleteConversationTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Common ──

    [Fact]
    public async Task DeleteConversation_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{Guid.NewGuid()}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task DeleteConversation_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, participantOne.AccessToken, participantTwo.UserId);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task DeleteConversation_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.DeleteAsync($"/api/conversations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Direct conversations ──

    [Fact]
    public async Task DeleteDirectConversation_ShouldReturn204()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var other = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, other.UserId);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteDirectConversation_WhenOneHides_OtherShouldStillHaveAccess()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var other = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, other.UserId);

        // Caller hides their side
        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}",
            caller.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Other participant should still be able to fetch messages
        var messagesResponse = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            other.AccessToken);
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteDirectConversation_WhenOneHides_ConversationDisappearsFromTheirList()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var other = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, other.UserId);

        // Caller hides the conversation
        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}",
            caller.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Caller's conversation list should no longer contain the hidden conversation
        var listResponse = await _client.SendAuthorizedGetAsync(
            "/api/conversations",
            caller.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<ListConversationsResponse>(TestContext.Current.CancellationToken);
        list.Should().NotBeNull();
        list!.Conversations.Should().NotContain(c => c.ConversationId == conversationId);
    }

    [Fact]
    public async Task DeleteDirectConversation_WhenOneHides_OtherStillSeesItInList()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var other = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, other.UserId);

        // Caller hides the conversation
        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}",
            caller.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Other participant should still see it in their list
        var listResponse = await _client.SendAuthorizedGetAsync(
            "/api/conversations",
            other.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<ListConversationsResponse>(TestContext.Current.CancellationToken);
        list.Should().NotBeNull();
        list!.Conversations.Should().Contain(c => c.ConversationId == conversationId);
    }

    [Fact]
    public async Task DeleteDirectConversation_WhenBothHide_ConversationStillExists()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var other = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, other.UserId);

        // Both hide
        var firstDelete = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}",
            caller.AccessToken);
        firstDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondDelete = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}",
            other.AccessToken);
        secondDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Conversation should disappear from both lists
        var callerListResponse = await _client.SendAuthorizedGetAsync(
            "/api/conversations",
            caller.AccessToken);
        var callerList = await callerListResponse.Content.ReadFromJsonAsync<ListConversationsResponse>(TestContext.Current.CancellationToken);
        callerList!.Conversations.Should().NotContain(c => c.ConversationId == conversationId);

        var otherListResponse = await _client.SendAuthorizedGetAsync(
            "/api/conversations",
            other.AccessToken);
        var otherList = await otherListResponse.Content.ReadFromJsonAsync<ListConversationsResponse>(TestContext.Current.CancellationToken);
        otherList!.Conversations.Should().NotContain(c => c.ConversationId == conversationId);
    }

    [Fact]
    public async Task DeleteDirectConversation_AfterHiding_CanReopenViaOpenConversation()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var other = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, other.UserId);

        // Caller hides
        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}",
            caller.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Caller reopens
        var reopenedId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, other.UserId);
        reopenedId.Should().Be(conversationId);

        // Conversation reappears in caller's list
        var listResponse = await _client.SendAuthorizedGetAsync(
            "/api/conversations",
            caller.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await listResponse.Content.ReadFromJsonAsync<ListConversationsResponse>(TestContext.Current.CancellationToken);
        list.Should().NotBeNull();
        list!.Conversations.Should().Contain(c => c.ConversationId == conversationId);
    }

    [Fact]
    public async Task DeleteDirectConversation_AfterHiding_ReceivesIncomingMessages()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var other = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, other.UserId);

        // Caller hides
        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}",
            caller.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Other sends a message
        await ConversationTestHelper.SendConversationMessageAsync(
            _client, conversationId, "Hello, are you there?", other.AccessToken);

        // Caller can still fetch messages (participant row still exists)
        var messagesResponse = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Group conversations ──

    [Fact]
    public async Task DeleteGroupConversation_WhenParticipantLeaves_ShouldReturn204()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var leaver = await AuthTestHelper.RegisterAsync(_client, $"leaver_{tag}");
        var other1 = await AuthTestHelper.RegisterAsync(_client, $"remain1_{tag}");
        var other2 = await AuthTestHelper.RegisterAsync(_client, $"remain2_{tag}");

        var group = await ConversationTestHelper.CreateGroupConversationAsync(
            _client,
            leaver.AccessToken,
            "Test group",
            [leaver.UserId, other1.UserId, other2.UserId]);

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{group.ConversationId}",
            leaver.AccessToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteGroupConversation_RemainingParticipantsShouldStillHaveAccess()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var leaver = await AuthTestHelper.RegisterAsync(_client, $"leaver_{tag}");
        var other1 = await AuthTestHelper.RegisterAsync(_client, $"remain1_{tag}");
        var other2 = await AuthTestHelper.RegisterAsync(_client, $"remain2_{tag}");

        var group = await ConversationTestHelper.CreateGroupConversationAsync(
            _client,
            leaver.AccessToken,
            "Test group",
            [leaver.UserId, other1.UserId, other2.UserId]);

        // Leaver leaves the group
        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{group.ConversationId}",
            leaver.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Remaining participants can still access the conversation
        var messagesResponse = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{group.ConversationId}/messages",
            other1.AccessToken);
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
