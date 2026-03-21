using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.AcknowledgeRead;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class AcknowledgeConversationReadEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AcknowledgeConversationReadEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AcknowledgeRead_WithMessageId_ShouldReturn204()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "ack this dm", caller.AccessToken);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/ack",
            new AcknowledgeReadRequest(message.MessageId),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WithNullMessageId_ShouldReturn204()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "ack all dm", caller.AccessToken);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/ack",
            new AcknowledgeReadRequest(null),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenCalledTwice_ShouldBeIdempotent()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "ack twice dm", caller.AccessToken);

        var firstResponse = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/ack",
            new AcknowledgeReadRequest(message.MessageId),
            caller.AccessToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/ack",
            new AcknowledgeReadRequest(message.MessageId),
            caller.AccessToken);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenConversationHasNoMessages_ShouldReturn204()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/ack",
            new AcknowledgeReadRequest(null),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{Guid.NewGuid()}/ack",
            new AcknowledgeReadRequest(null),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, participantOne.AccessToken, participantTwo.UserId);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/ack",
            new AcknowledgeReadRequest(null),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task AcknowledgeRead_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/ack",
            new AcknowledgeReadRequest(Guid.NewGuid().ToString()),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task AcknowledgeRead_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{Guid.NewGuid()}/ack",
            new AcknowledgeReadRequest(null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcknowledgeRead_WithInvalidConversationId_ShouldReturnBadRequest()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations/not-a-guid/ack",
            new AcknowledgeReadRequest(null),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task AcknowledgeRead_WithInvalidMessageId_ShouldReturnBadRequest()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{Guid.NewGuid()}/ack",
            new AcknowledgeReadRequest("not-a-guid"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

}
