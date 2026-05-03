using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Channels.GetPinnedMessages;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class GetPinnedMessagesEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GetPinnedMessagesEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Channel tests ──────────────────────────────────────────────

    [Fact]
    public async Task GetChannelPinnedMessages_WhenPinned_ShouldReturnThem()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "pin me", owner.AccessToken);

        await PinAsync(channelId, message.MessageId, owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/pins",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<GetPinnedMessagesResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.ChannelId.Should().Be(channelId);
        payload.Items.Should().HaveCount(1);
        payload.Items[0].MessageId.Should().Be(message.MessageId);
        payload.Items[0].Content.Should().Be("pin me");
        payload.Items[0].PinnedByUserId.Should().Be(owner.UserId);
    }

    [Fact]
    public async Task GetChannelPinnedMessages_WhenNone_ShouldReturnEmptyList()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/pins",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<GetPinnedMessagesResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChannelPinnedMessages_WhenCallerIsNotMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/pins",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetChannelPinnedMessages_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{Guid.NewGuid()}/pins",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetChannelPinnedMessages_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            $"/api/channels/{Guid.NewGuid()}/pins",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Conversation tests ─────────────────────────────────────────

    [Fact]
    public async Task GetConversationPinnedMessages_WhenPinned_ShouldReturnThem()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "pin this dm", caller.AccessToken);

        await PinConversationAsync(conversationId, message.MessageId, caller.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/pins",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<Harmonie.Application.Features.Conversations.GetPinnedMessages.GetConversationPinnedMessagesResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.ConversationId.Should().Be(conversationId);
        payload.Items.Should().HaveCount(1);
        payload.Items[0].MessageId.Should().Be(message.MessageId);
        payload.Items[0].Content.Should().Be("pin this dm");
        payload.Items[0].PinnedByUserId.Should().Be(caller.UserId);
    }

    [Fact]
    public async Task GetConversationPinnedMessages_WhenNone_ShouldReturnEmptyList()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/pins",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<Harmonie.Application.Features.Conversations.GetPinnedMessages.GetConversationPinnedMessagesResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConversationPinnedMessages_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, participantOne.AccessToken, participantTwo.UserId);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/pins",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetConversationPinnedMessages_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{Guid.NewGuid()}/pins",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetConversationPinnedMessages_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            $"/api/conversations/{Guid.NewGuid()}/pins",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private async Task PinAsync(Guid channelId, Guid messageId, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/channels/{channelId}/messages/{messageId}/pin");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task PinConversationAsync(Guid conversationId, Guid messageId, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/conversations/{conversationId}/messages/{messageId}/pin");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
