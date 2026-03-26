using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.ListConversations;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ConversationEndpointsTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConversationEndpointsTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenConversation_FirstRequest_ShouldCreateConversation()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(Guid.Parse(target.UserId)),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>();
        payload.Should().NotBeNull();
        payload!.Created.Should().BeTrue();
        payload.ConversationId.Should().NotBeNullOrWhiteSpace();
        payload.Type.Should().Be("direct");
        payload.ParticipantIds.Should().HaveCount(2).And.Contain(caller.UserId).And.Contain(target.UserId);
    }

    [Fact]
    public async Task OpenConversation_SecondRequestForSamePair_ShouldReturnExistingConversation()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);

        var firstResponse = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(Guid.Parse(target.UserId)),
            caller.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<OpenConversationResponse>();
        firstPayload.Should().NotBeNull();

        var secondResponse = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(Guid.Parse(caller.UserId)),
            target.AccessToken);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<OpenConversationResponse>();
        secondPayload.Should().NotBeNull();
        secondPayload!.Created.Should().BeFalse();
        secondPayload.ConversationId.Should().Be(firstPayload!.ConversationId);
    }

    [Fact]
    public async Task OpenConversation_WhenTargetUserDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(Guid.NewGuid()),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.User.NotFound);
    }

    [Fact]
    public async Task OpenConversation_WhenCallerTargetsSelf_ShouldReturnBadRequest()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(Guid.Parse(caller.UserId)),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.CannotOpenSelf);
    }

    [Fact]
    public async Task OpenConversation_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/conversations",
            new OpenConversationRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListConversations_WhenUserHasConversations_ShouldReturnOtherParticipants()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var targetOne = await AuthTestHelper.RegisterAsync(_client);
        var targetTwo = await AuthTestHelper.RegisterAsync(_client);

        var openFirstResponse = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(Guid.Parse(targetOne.UserId)),
            caller.AccessToken);
        openFirstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var openSecondResponse = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(Guid.Parse(targetTwo.UserId)),
            caller.AccessToken);
        openSecondResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await _client.SendAuthorizedGetAsync("/api/conversations", caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ListConversationsResponse>();
        payload.Should().NotBeNull();
        payload!.Conversations.Should().HaveCount(2);
        payload.Conversations.Should().Contain(x =>
            x.Participants.Any(p => p.UserId == targetOne.UserId && p.Username == targetOne.Username));
        payload.Conversations.Should().Contain(x =>
            x.Participants.Any(p => p.UserId == targetTwo.UserId && p.Username == targetTwo.Username));
        payload.Conversations.Should().OnlyContain(x => x.Type == "direct");
    }

    [Fact]
    public async Task ListConversations_WhenUserHasNoConversations_ShouldReturnEmptyArray()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedGetAsync("/api/conversations", caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ListConversationsResponse>();
        payload.Should().NotBeNull();
        payload!.Conversations.Should().BeEmpty();
    }

    [Fact]
    public async Task ListConversations_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
