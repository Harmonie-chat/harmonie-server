using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.SearchConversationMessages;
using Harmonie.Application.Features.Conversations.SendMessage;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SearchConversationMessagesEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SearchConversationMessagesEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchConversationMessages_WhenCallerIsParticipant_ShouldReturnMatchesWithAuthorContext()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        await SendConversationMessageAsync(conversationId, "deploy alpha", caller.AccessToken);
        await Task.Delay(20);
        await SendConversationMessageAsync(conversationId, "random chatter", caller.AccessToken);
        await Task.Delay(20);
        await SendConversationMessageAsync(conversationId, "deploy beta", target.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
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
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        await SendConversationMessageAsync(conversationId, "incident one", caller.AccessToken);
        await Task.Delay(20);
        await SendConversationMessageAsync(conversationId, "incident two", target.AccessToken);
        await Task.Delay(20);
        await SendConversationMessageAsync(conversationId, "incident three", caller.AccessToken);

        var firstResponse = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages/search?q=incident&limit=2",
            caller.AccessToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<SearchConversationMessagesResponse>();
        firstPayload.Should().NotBeNull();
        firstPayload!.Items.Select(item => item.Content).Should().Equal("incident two", "incident three");
        firstPayload.NextCursor.Should().NotBeNullOrWhiteSpace();

        var secondResponse = await _client.SendAuthorizedGetAsync(
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
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, participantOne.AccessToken, participantTwo.UserId);

        var response = await _client.SendAuthorizedGetAsync(
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

    private async Task SendConversationMessageAsync(Guid conversationId, string content, string accessToken)
    {
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
