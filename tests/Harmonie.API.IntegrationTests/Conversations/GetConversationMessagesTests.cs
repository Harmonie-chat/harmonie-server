using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.GetMessages;
using Harmonie.Infrastructure.Persistence.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class GetConversationMessagesTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GetConversationMessagesTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetConversationMessages_WhenCallerIsParticipant_ShouldReturnMessagesAscending()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "first direct", caller.AccessToken);
        await Task.Delay(20);
        await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "second direct", target.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.ConversationId.Should().Be(conversationId);
        payload.Items.Select(x => x.Content).Should().Equal("first direct", "second direct");
    }

    [Fact]
    public async Task GetConversationMessages_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task GetConversationMessages_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, participantOne.AccessToken, participantTwo.UserId);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task GetConversationMessages_WithCursorPagination_ShouldReturnNextPage()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "first page item", caller.AccessToken);
        await Task.Delay(20);
        await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "second page item", target.AccessToken);
        await Task.Delay(20);
        await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "third page item", caller.AccessToken);

        var firstResponse = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages?limit=2",
            caller.AccessToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        firstPayload.Should().NotBeNull();
        firstPayload!.Items.Select(x => x.Content).Should().Equal("second page item", "third page item");
        firstPayload.NextCursor.Should().NotBeNullOrWhiteSpace();

        var secondResponse = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages?cursor={Uri.EscapeDataString(firstPayload.NextCursor!)}&limit=2",
            caller.AccessToken);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        secondPayload.Should().NotBeNull();
        secondPayload!.Items.Select(x => x.Content).Should().Equal("first page item");
        secondPayload.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetConversationMessages_ShouldExcludeSoftDeletedMessages()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        var visibleMessage = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "visible direct", caller.AccessToken);
        await Task.Delay(20);
        var deletedMessage = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "deleted direct", target.AccessToken);

        await SoftDeleteConversationMessageAsync(deletedMessage.MessageId);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items[0].MessageId.Should().Be(visibleMessage.MessageId);
        payload.Items[0].Content.Should().Be("visible direct");
    }

    [Fact]
    public async Task GetConversationMessages_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task SoftDeleteConversationMessageAsync(string messageId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbSession = scope.ServiceProvider.GetRequiredService<DbSession>();
        var connection = await dbSession.GetOpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
                              UPDATE messages
                              SET deleted_at_utc = @DeletedAtUtc
                              WHERE id = @MessageId
                              """;
        command.Parameters.AddWithValue("DeletedAtUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("MessageId", Guid.Parse(messageId));
        await command.ExecuteNonQueryAsync();
    }
}
