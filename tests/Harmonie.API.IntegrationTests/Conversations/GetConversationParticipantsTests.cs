using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.GetConversationParticipants;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Xunit;

namespace Harmonie.API.IntegrationTests.Conversations;

public sealed class GetConversationParticipantsTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GetConversationParticipantsTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetConversationParticipants_WhenParticipantQueries_ShouldReturn200WithParticipants()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAndGetIdAsync(caller.AccessToken, target.UserId);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/participants",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetConversationParticipantsResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Participants.Should().NotBeNull();
        payload.Participants.Should().HaveCount(2);

        var userIds = payload.Participants.Select(p => p.UserId).ToArray();
        userIds.Should().Contain(caller.UserId);
        userIds.Should().Contain(target.UserId);

        foreach (var participant in payload.Participants)
        {
            participant.Username.Should().NotBeNullOrEmpty();
            participant.JoinedAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5));
            participant.IsHidden.Should().BeFalse();
        }
    }

    [Fact]
    public async Task GetConversationParticipants_WhenUserIsNotParticipant_ShouldReturn403()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAndGetIdAsync(caller.AccessToken, target.UserId);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/participants",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task GetConversationParticipants_WhenConversationDoesNotExist_ShouldReturn404()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{Guid.NewGuid()}/participants",
            user.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task GetConversationParticipants_WhenNotAuthenticated_ShouldReturn401()
    {
        var response = await _client.GetAsync(
            $"/api/conversations/{Guid.NewGuid()}/participants",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> OpenConversationAndGetIdAsync(string accessToken, Guid targetUserId)
    {
        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(targetUserId),
            accessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>(TestContext.Current.CancellationToken);
        return payload!.ConversationId;
    }
}
