using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ConversationSendMessageRequest = Harmonie.Application.Features.Conversations.SendMessage.SendMessageRequest;
using ConversationSendMessageResponse = Harmonie.Application.Features.Conversations.SendMessage.SendMessageResponse;

namespace Harmonie.API.IntegrationTests;

public sealed class RemoveReactionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RemoveReactionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Channel reaction tests ─────────────────────────────────────

    [Fact]
    public async Task RemoveChannelReaction_WhenCallerIsMember_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);
        var message = await SendChannelMessageAsync(channelId, "react then remove", owner.AccessToken);

        await SendAuthorizedPutAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            owner.AccessToken);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveChannelReaction_WhenReactionDoesNotExist_ShouldBeIdempotent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);
        var message = await SendChannelMessageAsync(channelId, "no reaction here", owner.AccessToken);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveChannelReaction_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reactions/%F0%9F%91%8D",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task RemoveChannelReaction_WhenCallerIsNotMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);
        var message = await SendChannelMessageAsync(channelId, "can't remove", owner.AccessToken);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task RemoveChannelReaction_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await CreateGuildAndChannelAsync(owner.AccessToken);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{Guid.NewGuid()}/reactions/%F0%9F%91%8D",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    [Fact]
    public async Task RemoveChannelReaction_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.DeleteAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reactions/%F0%9F%91%8D");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Conversation reaction tests ────────────────────────────────

    [Fact]
    public async Task RemoveConversationReaction_WhenCallerIsParticipant_ShouldReturn204()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "react then remove dm", caller.AccessToken);

        await SendAuthorizedPutAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            caller.AccessToken);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveConversationReaction_WhenReactionDoesNotExist_ShouldBeIdempotent()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "no reaction dm", caller.AccessToken);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveConversationReaction_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reactions/%E2%9D%A4",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task RemoveConversationReaction_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);
        var message = await SendConversationMessageAsync(conversationId, "private dm", participantOne.AccessToken);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task RemoveConversationReaction_WhenMessageDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await OpenConversationAsync(caller.AccessToken, target.UserId);

        var response = await _client.SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{Guid.NewGuid()}/reactions/%E2%9D%A4",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Reaction.MessageNotFound);
    }

    [Fact]
    public async Task RemoveConversationReaction_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.DeleteAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reactions/%E2%9D%A4");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private async Task<(string GuildId, string ChannelId)> CreateGuildAndChannelAsync(string accessToken)
    {
        var guildName = $"guild{Guid.NewGuid():N}"[..16];
        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(guildName),
            accessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guildPayload.Should().NotBeNull();

        var createChannelResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guildPayload!.GuildId}/channels",
            new CreateChannelRequest($"chan{Guid.NewGuid():N}"[..16], ChannelTypeInput.Text, 1),
            accessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var channelPayload = await createChannelResponse.Content.ReadFromJsonAsync<CreateChannelResponse>();
        channelPayload.Should().NotBeNull();

        return (guildPayload.GuildId, channelPayload!.ChannelId);
    }

    private async Task<SendMessageResponse> SendChannelMessageAsync(
        string channelId,
        string content,
        string accessToken)
    {
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<string> OpenConversationAsync(string accessToken, string targetUserId)
    {
        var response = await _client.SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(targetUserId),
            accessToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>();
        payload.Should().NotBeNull();
        return payload!.ConversationId;
    }

    private async Task<ConversationSendMessageResponse> SendConversationMessageAsync(
        string conversationId,
        string content,
        string accessToken)
    {
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new ConversationSendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<ConversationSendMessageResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedPutAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
