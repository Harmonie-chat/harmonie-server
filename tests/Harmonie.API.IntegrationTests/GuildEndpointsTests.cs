using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.InviteMember;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class GuildEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GuildEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GuildPrimaryFlow_CreateInviteSendRead_ShouldSucceed()
    {
        var userA = await RegisterAsync();
        var userB = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Harmonie Guild"),
            userA.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();
        if (createGuildPayload is null)
            throw new InvalidOperationException("Create guild payload is null.");

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(userB.UserId),
            userA.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteMemberResponse>();
        invitePayload.Should().NotBeNull();
        if (invitePayload is null)
            throw new InvalidOperationException("Invite member payload is null.");

        invitePayload.UserId.Should().Be(userB.UserId);
        invitePayload.Role.Should().Be("Member");

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            userB.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();
        if (channelsPayload is null)
            throw new InvalidOperationException("Guild channels payload is null.");

        channelsPayload.Channels.Should().HaveCount(2);
        channelsPayload.Channels.Should().Contain(channel => channel.Name == "general" && channel.Type == "Text");
        channelsPayload.Channels.Should().Contain(channel => channel.Name == "General Voice" && channel.Type == "Voice");

        var textChannel = channelsPayload.Channels.First(channel => channel.Type == "Text");

        var sendMessageResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("Hello team"),
            userA.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendMessagePayload.Should().NotBeNull();
        if (sendMessagePayload is null)
            throw new InvalidOperationException("Send message payload is null.");

        sendMessagePayload.Content.Should().Be("Hello team");

        var getMessagesResponse = await SendAuthorizedGetAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            userB.AccessToken);
        getMessagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getMessagesPayload = await getMessagesResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        getMessagesPayload.Should().NotBeNull();
        if (getMessagesPayload is null)
            throw new InvalidOperationException("Get messages payload is null.");

        getMessagesPayload.Items.Should().Contain(item => item.MessageId == sendMessagePayload.MessageId);
        getMessagesPayload.Items.Should().Contain(item => item.Content == "Hello team");
    }

    [Fact]
    public async Task InviteMember_WhenNonAdminInvites_ShouldReturnForbidden()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();
        var target = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();
        if (createGuildPayload is null)
            throw new InvalidOperationException("Create guild payload is null.");

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(target.UserId),
            outsider.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        if (error is null)
            throw new InvalidOperationException("Invite forbidden payload is null.");

        error.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
    }

    [Fact]
    public async Task SendMessage_WhenChannelIsVoice_ShouldReturnConflict()
    {
        var user = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Voice Guild"),
            user.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();
        if (createGuildPayload is null)
            throw new InvalidOperationException("Create guild payload is null.");

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            user.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();
        if (channelsPayload is null)
            throw new InvalidOperationException("Channels payload is null.");

        var voiceChannel = channelsPayload.Channels.First(channel => channel.Type == "Voice");

        var sendMessageResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{voiceChannel.ChannelId}/messages",
            new SendMessageRequest("Should fail"),
            user.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await sendMessageResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        if (error is null)
            throw new InvalidOperationException("Send message error payload is null.");

        error.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
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
        if (payload is null)
            throw new InvalidOperationException("Register payload is null.");

        return payload;
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
}
