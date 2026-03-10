using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Conversations.DeleteDirectMessage;
using Harmonie.Application.Features.Conversations.EditDirectMessage;
using Harmonie.Application.Features.Conversations.GetDirectMessages;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SendDirectMessage;
using Harmonie.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class DirectMessageEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DirectMessageEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendDirectMessage_WhenCallerIsParticipant_ShouldCreateMessage()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        var response = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendDirectMessageRequest("hello direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendDirectMessageResponse>();
        payload.Should().NotBeNull();
        payload!.ConversationId.Should().Be(conversationId);
        payload.AuthorUserId.Should().Be(caller.UserId);
        payload.Content.Should().Be("hello direct");
    }

    [Fact]
    public async Task SendDirectMessage_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();

        var response = await SendAuthorizedPostAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages",
            new SendDirectMessageRequest("hello direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task SendDirectMessage_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);

        var response = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendDirectMessageRequest("intrusion"),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task SendDirectMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages",
            new SendDirectMessageRequest("hello direct"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDirectMessages_WhenCallerIsParticipant_ShouldReturnMessagesAscending()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        await SendDirectMessageAsync(conversationId, "first direct", caller.AccessToken);
        await Task.Delay(20);
        await SendDirectMessageAsync(conversationId, "second direct", target.AccessToken);

        var response = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetDirectMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.ConversationId.Should().Be(conversationId);
        payload.Items.Select(x => x.Content).Should().Equal("first direct", "second direct");
    }

    [Fact]
    public async Task GetDirectMessages_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();

        var response = await SendAuthorizedGetAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task GetDirectMessages_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);

        var response = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task GetDirectMessages_WithCursorPagination_ShouldReturnNextPage()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        await SendDirectMessageAsync(conversationId, "first page item", caller.AccessToken);
        await Task.Delay(20);
        await SendDirectMessageAsync(conversationId, "second page item", target.AccessToken);
        await Task.Delay(20);
        await SendDirectMessageAsync(conversationId, "third page item", caller.AccessToken);

        var firstResponse = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages?limit=2",
            caller.AccessToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<GetDirectMessagesResponse>();
        firstPayload.Should().NotBeNull();
        firstPayload!.Items.Select(x => x.Content).Should().Equal("second page item", "third page item");
        firstPayload.NextCursor.Should().NotBeNullOrWhiteSpace();

        var secondResponse = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages?cursor={Uri.EscapeDataString(firstPayload.NextCursor!)}&limit=2",
            caller.AccessToken);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<GetDirectMessagesResponse>();
        secondPayload.Should().NotBeNull();
        secondPayload!.Items.Select(x => x.Content).Should().Equal("first page item");
        secondPayload.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetDirectMessages_ShouldExcludeSoftDeletedMessages()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        var visibleMessage = await SendDirectMessageAsync(conversationId, "visible direct", caller.AccessToken);
        await Task.Delay(20);
        var deletedMessage = await SendDirectMessageAsync(conversationId, "deleted direct", target.AccessToken);

        await SoftDeleteDirectMessageAsync(deletedMessage.MessageId);

        var response = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetDirectMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items[0].MessageId.Should().Be(visibleMessage.MessageId);
        payload.Items[0].Content.Should().Be("visible direct");
    }

    [Fact]
    public async Task GetDirectMessages_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EditDirectMessage_WhenAuthorEditsOwnMessage_ShouldReturnUpdatedMessage()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var message = await SendDirectMessageAsync(conversationId, "original direct", caller.AccessToken);

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            new EditDirectMessageRequest("updated direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<EditDirectMessageResponse>();
        payload.Should().NotBeNull();
        payload!.MessageId.Should().Be(message.MessageId);
        payload.ConversationId.Should().Be(conversationId);
        payload.AuthorUserId.Should().Be(caller.UserId);
        payload.Content.Should().Be("updated direct");
        payload.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task EditDirectMessage_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}",
            new EditDirectMessageRequest("updated direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task EditDirectMessage_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendDirectMessageAsync(conversationId, "original direct", participantOne.AccessToken);

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            new EditDirectMessageRequest("intrusion"),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task EditDirectMessage_WhenCallerIsNotAuthor_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendDirectMessageAsync(conversationId, "original direct", participantOne.AccessToken);

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            new EditDirectMessageRequest("edited by someone else"),
            participantTwo.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.EditForbidden);
    }

    [Fact]
    public async Task EditDirectMessage_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        var response = await SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}/messages/{Guid.NewGuid()}",
            new EditDirectMessageRequest("updated direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task EditDirectMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}",
            new EditDirectMessageRequest("updated direct"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteDirectMessage_WhenAuthorDeletesOwnMessage_ShouldReturn204()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var message = await SendDirectMessageAsync(conversationId, "delete me", caller.AccessToken);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteDirectMessage_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task DeleteDirectMessage_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendDirectMessageAsync(conversationId, "delete me", participantOne.AccessToken);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task DeleteDirectMessage_WhenCallerIsNotAuthor_ShouldReturnForbidden()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendDirectMessageAsync(conversationId, "delete me", participantOne.AccessToken);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}",
            participantTwo.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task DeleteDirectMessage_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{Guid.NewGuid()}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task DeleteDirectMessage_WhenDeleted_ShouldBeExcludedFromReadEndpoint()
    {
        var caller = await RegisterAsync();
        var target = await RegisterAsync();
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var visibleMessage = await SendDirectMessageAsync(conversationId, "keep me", target.AccessToken);
        await Task.Delay(20);
        var deletedMessage = await SendDirectMessageAsync(conversationId, "delete me", caller.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{deletedMessage.MessageId}",
            caller.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await listResponse.Content.ReadFromJsonAsync<GetDirectMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items[0].MessageId.Should().Be(visibleMessage.MessageId);
    }

    [Fact]
    public async Task DeleteDirectMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.DeleteAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> OpenConversationAsync(string accessToken, string targetUserId)
    {
        var response = await SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(targetUserId),
            accessToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>();
        payload.Should().NotBeNull();
        return payload!.ConversationId;
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

    private async Task<SendDirectMessageResponse> SendDirectMessageAsync(
        string conversationId,
        string content,
        string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendDirectMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendDirectMessageResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedPostAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPatchAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedDeleteAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task SoftDeleteDirectMessageAsync(string messageId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              UPDATE direct_messages
                              SET deleted_at_utc = @DeletedAtUtc
                              WHERE id = @MessageId
                              """;
        command.Parameters.AddWithValue("DeletedAtUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("MessageId", Guid.Parse(messageId));
        await command.ExecuteNonQueryAsync();
    }
}
