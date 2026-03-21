using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.InviteMember;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class DeleteMessageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DeleteMessageTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteMessage_WhenAuthorDeletesOwnMessage_ShouldReturn204()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "delete-message-channel");
        var messageId = await SendMessageAndGetIdAsync(channelId, "message to delete", author.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMessage_WhenAdminDeletesAnotherUsersMessage_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Admin Delete Message Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "admin-delete-channel", guildId: guildId);
        var messageId = await SendMessageAndGetIdAsync(channelId, "member's message", member.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMessage_WhenMemberTriesToDeleteAnotherUsersMessage_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Forbidden Delete Message Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "member-delete-msg-channel", guildId: guildId);
        var messageId = await SendMessageAndGetIdAsync(channelId, "owner's message", owner.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
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
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "outsider-delete-msg-channel");
        var messageId = await SendMessageAndGetIdAsync(channelId, "owner's message", owner.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
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
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "delete-notfound-channel");
        var nonExistentMessageId = Guid.NewGuid();

        var deleteResponse = await SendAuthorizedDeleteAsync(
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
        var author = await RegisterAsync();
        var nonExistentChannelId = Guid.NewGuid();
        var nonExistentMessageId = Guid.NewGuid();

        var deleteResponse = await SendAuthorizedDeleteAsync(
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

    // ─── Helpers ───────────────────────────────────────────────────────────────

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

    private async Task<string> CreateGuildAndGetIdAsync(string accessToken, string guildName)
    {
        var response = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(guildName),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateGuildResponse>();
        payload.Should().NotBeNull();

        return payload!.GuildId;
    }

    private async Task InviteMemberAsync(string guildId, string userId, string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/members/invite",
            new InviteMemberRequest(userId),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> CreateChannelAndGetIdAsync(
        string accessToken,
        string name,
        string? guildId = null,
        int position = 0)
    {
        if (guildId is null)
        {
            guildId = await CreateGuildAndGetIdAsync(accessToken, $"Guild for {name}");
        }

        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            new CreateChannelRequest(name, ChannelTypeInput.Text, position),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateChannelResponse>();
        payload.Should().NotBeNull();

        return payload!.ChannelId;
    }

    private async Task<string> SendMessageAndGetIdAsync(
        string channelId,
        string content,
        string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();

        return payload!.MessageId;
    }

    private async Task<HttpResponseMessage> SendAuthorizedPostAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
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
}
