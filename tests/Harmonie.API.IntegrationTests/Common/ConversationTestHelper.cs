using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Conversations.CreateGroupConversation;
using Harmonie.Application.Features.Conversations.OpenConversation;

namespace Harmonie.API.IntegrationTests.Common;

public static class ConversationTestHelper
{
    public static async Task<Guid> OpenConversationAsync(
        HttpClient client,
        string accessToken,
        Guid targetUserId)
    {
        var response = await client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(targetUserId),
            accessToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>();
        payload.Should().NotBeNull();
        return payload!.ConversationId;
    }

    public static async Task<CreateGroupConversationResponse> CreateGroupConversationAsync(
        HttpClient client,
        string accessToken,
        string? name,
        IReadOnlyList<Guid> participantUserIds)
    {
        var response = await client.SendAuthorizedPostAsync(
            "/api/conversations/group",
            new CreateGroupConversationRequest(name, participantUserIds.ToList()),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateGroupConversationResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    public static async Task<SendMessageResponse> SendConversationMessageAsync(
        HttpClient client,
        Guid conversationId,
        string content,
        string accessToken)
    {
        var response = await client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    public static async Task AddReactionAsync(
        HttpClient client,
        Guid conversationId,
        Guid messageId,
        string urlEncodedEmoji,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/conversations/{conversationId}/messages/{messageId}/reactions/{urlEncodedEmoji}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
