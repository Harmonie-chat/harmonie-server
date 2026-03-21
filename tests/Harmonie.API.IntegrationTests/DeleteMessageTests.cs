using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class DeleteMessageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DeleteMessageTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteMessage_WhenAuthorDeletesOwnMessage_ShouldReturn204()
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, "delete-message-channel");
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "message to delete", author.AccessToken);

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMessage_WhenAdminDeletesAnotherUsersMessage_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Admin Delete Message Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, member.UserId, owner.AccessToken);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "admin-delete-channel", guildId: guildId);
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "member's message", member.AccessToken);

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMessage_WhenMemberTriesToDeleteAnotherUsersMessage_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Forbidden Delete Message Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, member.UserId, owner.AccessToken);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "member-delete-msg-channel", guildId: guildId);
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "owner's message", owner.AccessToken);

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            member.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task DeleteMessage_WhenNonMemberTriesToDelete_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "outsider-delete-msg-channel");
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "owner's message", owner.AccessToken);

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            outsider.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task DeleteMessage_WhenMessageNotFound_ShouldReturn404()
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, "delete-notfound-channel");
        var nonExistentMessageId = Guid.NewGuid();

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{nonExistentMessageId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task DeleteMessage_WhenChannelNotFound_ShouldReturn404()
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentChannelId = Guid.NewGuid();
        var nonExistentMessageId = Guid.NewGuid();

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{nonExistentChannelId}/messages/{nonExistentMessageId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task DeleteMessage_WhenNotAuthenticated_ShouldReturn401()
    {
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var deleteResponse = await _client.DeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
