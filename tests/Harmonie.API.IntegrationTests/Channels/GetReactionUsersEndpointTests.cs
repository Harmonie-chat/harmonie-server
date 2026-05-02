using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ChannelGetReactionUsersResponse = Harmonie.Application.Features.Channels.GetReactionUsers.GetReactionUsersResponse;
using ConversationGetReactionUsersResponse = Harmonie.Application.Features.Conversations.GetReactionUsers.GetReactionUsersResponse;

namespace Harmonie.API.IntegrationTests;

public sealed class GetReactionUsersEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GetReactionUsersEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Channel tests ──────────────────────────────────────────────

    [Fact]
    public async Task GetChannelReactionUsers_WhenNoReactions_ShouldReturnEmptyUsers()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "no reactions", owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/thumbsup/users",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ChannelGetReactionUsersResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Users.Should().BeEmpty();
        payload.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetChannelReactionUsers_WithReactions_ShouldReturnPaginatedUsers()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "react to this", owner.AccessToken);

        await ChannelTestHelper.AddReactionAsync(_client, channelId, message.MessageId, "thumbsup", owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/thumbsup/users",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ChannelGetReactionUsersResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Users.Should().HaveCount(1);
        payload.TotalCount.Should().Be(1);
        payload.Users[0].UserId.Should().Be(owner.UserId);
    }

    [Fact]
    public async Task GetChannelReactionUsers_WhenChannelDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reactions/thumbsup/users",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetChannelReactionUsers_WhenCallerIsNotMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var (_, channelId) = await ChannelTestHelper.CreateGuildAndChannelAsync(_client, owner.AccessToken);
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "can't access", owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/thumbsup/users",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Conversation tests ─────────────────────────────────────────

    [Fact]
    public async Task GetConversationReactionUsers_WhenNoReactions_ShouldReturnEmptyUsers()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "no reactions dm", caller.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/heart/users",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ConversationGetReactionUsersResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Users.Should().BeEmpty();
        payload.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetConversationReactionUsers_WithReactions_ShouldReturnPaginatedUsers()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "react dm", caller.AccessToken);

        await ConversationTestHelper.AddReactionAsync(_client, conversationId, message.MessageId, "heart", caller.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/heart/users",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ConversationGetReactionUsersResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Users.Should().HaveCount(1);
        payload.TotalCount.Should().Be(1);
        payload.Users[0].UserId.Should().Be(caller.UserId);
    }

    [Fact]
    public async Task GetConversationReactionUsers_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, participantOne.AccessToken, participantTwo.UserId);
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "private dm", participantOne.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/heart/users",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
