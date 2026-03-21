using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ChannelGetMessagesResponse = Harmonie.Application.Features.Channels.GetMessages.GetMessagesResponse;
using ConversationGetMessagesResponse = Harmonie.Application.Features.Conversations.GetMessages.GetMessagesResponse;
using ConversationSendMessageRequest = Harmonie.Application.Features.Conversations.SendMessage.SendMessageRequest;
using ConversationSendMessageResponse = Harmonie.Application.Features.Conversations.SendMessage.SendMessageResponse;

namespace Harmonie.API.IntegrationTests;

public sealed class GetMessagesReactionsTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GetMessagesReactionsTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Channel tests ──────────────────────────────────────────────

    [Fact]
    public async Task GetChannelMessages_WhenNoReactions_ShouldReturnEmptyReactionsArray()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "no reactions here", owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ChannelGetMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items[0].Reactions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChannelMessages_WithReactions_ShouldIncludeReactionData()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "react to this", owner.AccessToken);

        await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ChannelGetMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();

        var reactions = payload.Items[0].Reactions;
        reactions.Should().ContainSingle();
        reactions[0].Emoji.Should().Be("\U0001f44d");
        reactions[0].Count.Should().Be(1);
        reactions[0].ReactedByMe.Should().BeTrue();
    }

    [Fact]
    public async Task GetChannelMessages_ReactedByMe_ShouldReflectCallerPerspective()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var (guildId, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        await GuildTestHelper.InviteMemberAsync(_client, guildId, member.UserId, owner.AccessToken);

        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "who reacted?", owner.AccessToken);

        // Owner reacts, member does not
        await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/%F0%9F%91%8D",
            owner.AccessToken);

        // Owner sees reactedByMe = true
        var ownerResponse = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            owner.AccessToken);
        var ownerPayload = await ownerResponse.Content.ReadFromJsonAsync<ChannelGetMessagesResponse>();
        ownerPayload!.Items[0].Reactions[0].ReactedByMe.Should().BeTrue();

        // Member sees reactedByMe = false
        var memberResponse = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            member.AccessToken);
        var memberPayload = await memberResponse.Content.ReadFromJsonAsync<ChannelGetMessagesResponse>();
        memberPayload!.Items[0].Reactions[0].ReactedByMe.Should().BeFalse();
        memberPayload.Items[0].Reactions[0].Count.Should().Be(1);
    }

    // ─── Conversation tests ─────────────────────────────────────────

    [Fact]
    public async Task GetConversationMessages_WhenNoReactions_ShouldReturnEmptyReactionsArray()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        await SendConversationMessageAsync(conversationId, "no reactions dm", caller.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ConversationGetMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();
        payload.Items[0].Reactions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConversationMessages_WithReactions_ShouldIncludeReactionData()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "react dm", caller.AccessToken);

        await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            caller.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ConversationGetMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.Items.Should().ContainSingle();

        var reactions = payload.Items[0].Reactions;
        reactions.Should().ContainSingle();
        reactions[0].Emoji.Should().Be("\u2764");
        reactions[0].Count.Should().Be(1);
        reactions[0].ReactedByMe.Should().BeTrue();
    }

    [Fact]
    public async Task GetConversationMessages_ReactedByMe_ShouldReflectCallerPerspective()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await SendConversationMessageAsync(conversationId, "perspective dm", caller.AccessToken);

        // Caller reacts, target does not
        await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/%E2%9D%A4",
            caller.AccessToken);

        // Caller sees reactedByMe = true
        var callerResponse = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);
        var callerPayload = await callerResponse.Content.ReadFromJsonAsync<ConversationGetMessagesResponse>();
        callerPayload!.Items[0].Reactions[0].ReactedByMe.Should().BeTrue();

        // Target sees reactedByMe = false
        var targetResponse = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            target.AccessToken);
        var targetPayload = await targetResponse.Content.ReadFromJsonAsync<ConversationGetMessagesResponse>();
        targetPayload!.Items[0].Reactions[0].ReactedByMe.Should().BeFalse();
        targetPayload.Items[0].Reactions[0].Count.Should().Be(1);
    }

    // ─── Helpers ────────────────────────────────────────────────────

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

    private async Task<HttpResponseMessage> SendAuthorizedPutNoBodyAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
