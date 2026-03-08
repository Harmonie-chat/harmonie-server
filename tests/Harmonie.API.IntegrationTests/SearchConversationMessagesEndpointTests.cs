using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SearchConversationMessages;
using Harmonie.Application.Features.Conversations.SendDirectMessage;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SearchConversationMessagesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SearchConversationMessagesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchConversationMessages_WhenCallerIsParticipant_ShouldReturnMatchesWithAuthorContext()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        await SendDirectMessageAsync(conversationId, "deploy alpha", caller.AccessToken);
        await Task.Delay(20);
        await SendDirectMessageAsync(conversationId, "random chatter", caller.AccessToken);
        await Task.Delay(20);
        await SendDirectMessageAsync(conversationId, "deploy beta", target.AccessToken);

        var response = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages/search?q=deploy",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SearchConversationMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.ConversationId.Should().Be(conversationId);
        payload.Items.Select(item => item.Content).Should().Equal("deploy alpha", "deploy beta");
        payload.Items[0].AuthorUsername.Should().Be(caller.Username);
        payload.Items[1].AuthorUsername.Should().Be(target.Username);
    }

    [Fact]
    public async Task SearchConversationMessages_WithCursorAndDateRange_ShouldReturnNextPage()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        await SendDirectMessageAsync(conversationId, "incident one", caller.AccessToken);
        await Task.Delay(20);
        await SendDirectMessageAsync(conversationId, "incident two", target.AccessToken);
        await Task.Delay(20);
        await SendDirectMessageAsync(conversationId, "incident three", caller.AccessToken);

        var firstResponse = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages/search?q=incident&limit=2",
            caller.AccessToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<SearchConversationMessagesResponse>();
        firstPayload.Should().NotBeNull();
        firstPayload!.Items.Select(item => item.Content).Should().Equal("incident two", "incident three");
        firstPayload.NextCursor.Should().NotBeNullOrWhiteSpace();

        var secondResponse = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages/search?q=incident&limit=2&cursor={Uri.EscapeDataString(firstPayload.NextCursor!)}",
            caller.AccessToken);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<SearchConversationMessagesResponse>();
        secondPayload.Should().NotBeNull();
        secondPayload!.Items.Select(item => item.Content).Should().Equal("incident one");
        secondPayload.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task SearchConversationMessages_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);

        var response = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages/search?q=incident",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task SearchConversationMessages_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/search?q=incident");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<RegisterResponse> RegisterAsync()
    {
        var request = new RegisterRequest(
            Email: $"test{Guid.NewGuid():N}@harmonie.chat",
            Username: $"user{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<string> OpenConversationAsync(string accessToken, string targetUserId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/conversations")
        {
            Content = JsonContent.Create(new OpenConversationRequest(targetUserId))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>();
        payload.Should().NotBeNull();
        return payload!.ConversationId;
    }

    private async Task SendDirectMessageAsync(string conversationId, string content, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conversationId}/messages")
        {
            Content = JsonContent.Create(new SendDirectMessageRequest(content))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(string uri, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
