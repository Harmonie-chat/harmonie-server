using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.CreateGroupConversation;
using Harmonie.Application.Features.Conversations.ListConversations;
using Harmonie.Application.Features.Conversations.SendMessage;
using Xunit;

namespace Harmonie.API.IntegrationTests.Conversations;

public sealed class GroupConversationEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GroupConversationEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateGroupConversation_WithValidRequest_ShouldReturn201WithGroupType()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var memberB = await AuthTestHelper.RegisterAsync(_client);
        var memberC = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations/group",
            new CreateGroupConversationRequest("Dev Team", [
                caller.UserId,
                memberB.UserId,
                memberC.UserId
            ]),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateGroupConversationResponse>();
        payload.Should().NotBeNull();
        payload!.Type.Should().Be("group");
        payload.Name.Should().Be("Dev Team");
        payload.ConversationId.Should().NotBeEmpty();
        payload.ParticipantIds.Should().HaveCount(3)
            .And.Contain(caller.UserId)
            .And.Contain(memberB.UserId)
            .And.Contain(memberC.UserId);
    }

    [Fact]
    public async Task CreateGroupConversation_WhenCallerIsNotInList_ShouldReturn403()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var memberA = await AuthTestHelper.RegisterAsync(_client);
        var memberB = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations/group",
            new CreateGroupConversationRequest("Dev Team", [
                memberA.UserId,
                memberB.UserId
            ]),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task CreateGroupConversation_WhenParticipantDoesNotExist_ShouldReturn404()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations/group",
            new CreateGroupConversationRequest(null, [
                caller.UserId,
                Guid.NewGuid()
            ]),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
    }

    [Fact]
    public async Task CreateGroupConversation_WithDuplicateParticipants_ShouldReturn400()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations/group",
            new CreateGroupConversationRequest(null, [
                caller.UserId,
                caller.UserId
            ]),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task CreateGroupConversation_TwiceWithSameParticipants_ShouldCreateTwoDistinctGroups()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var memberB = await AuthTestHelper.RegisterAsync(_client);

        var firstPayload = await ConversationTestHelper.CreateGroupConversationAsync(
            _client, caller.AccessToken, "Group One",
            [caller.UserId, memberB.UserId]);

        var secondPayload = await ConversationTestHelper.CreateGroupConversationAsync(
            _client, caller.AccessToken, "Group Two",
            [caller.UserId, memberB.UserId]);

        firstPayload.ConversationId.Should().NotBe(secondPayload.ConversationId);
    }

    [Fact]
    public async Task SendMessage_ByGroupMember_ShouldReturn201()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var memberB = await AuthTestHelper.RegisterAsync(_client);

        var group = await ConversationTestHelper.CreateGroupConversationAsync(
            _client, caller.AccessToken, null,
            [caller.UserId, memberB.UserId]);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{group.ConversationId}/messages",
            new SendMessageRequest("hello group"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SendMessage_ByNonMember_ShouldReturn403()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var memberB = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);

        var group = await ConversationTestHelper.CreateGroupConversationAsync(
            _client, caller.AccessToken, null,
            [caller.UserId, memberB.UserId]);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{group.ConversationId}/messages",
            new SendMessageRequest("hello group"),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListConversations_ShouldIncludeCreatedGroup()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var memberB = await AuthTestHelper.RegisterAsync(_client);

        var group = await ConversationTestHelper.CreateGroupConversationAsync(
            _client, caller.AccessToken, "My Group",
            [caller.UserId, memberB.UserId]);

        var response = await _client.SendAuthorizedGetAsync("/api/conversations", caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ListConversationsResponse>();
        payload.Should().NotBeNull();
        payload!.Conversations.Should().Contain(x =>
            x.ConversationId == group.ConversationId
            && x.Type == "group"
            && x.Name == "My Group"
            && x.Participants.Any(p => p.UserId == caller.UserId)
            && x.Participants.Any(p => p.UserId == memberB.UserId));
    }
}
