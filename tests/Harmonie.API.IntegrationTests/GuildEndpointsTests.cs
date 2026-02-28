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
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
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

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(userB.UserId),
            userA.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteMemberResponse>();
        invitePayload.Should().NotBeNull();

        invitePayload!.UserId.Should().Be(userB.UserId);
        invitePayload.Role.Should().Be("Member");

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            userB.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        channelsPayload!.Channels.Should().HaveCount(2);
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

        sendMessagePayload!.Content.Should().Be("Hello team");

        var getMessagesResponse = await SendAuthorizedGetAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            userB.AccessToken);
        getMessagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getMessagesPayload = await getMessagesResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        getMessagesPayload.Should().NotBeNull();

        getMessagesPayload!.Items.Should().Contain(item => item.MessageId == sendMessagePayload.MessageId);
        getMessagesPayload.Items.Should().Contain(item => item.Content == "Hello team");
    }

    [Fact]
    public async Task ListUserGuilds_ShouldReturnOwnedAndInvitedGuilds()
    {
        var owner = await RegisterAsync();
        var inviter = await RegisterAsync();

        var ownerGuildOneResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Guild One"),
            owner.AccessToken);
        ownerGuildOneResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var ownerGuildOne = await ownerGuildOneResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        ownerGuildOne.Should().NotBeNull();

        var ownerGuildTwoResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Guild Two"),
            owner.AccessToken);
        ownerGuildTwoResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var ownerGuildTwo = await ownerGuildTwoResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        ownerGuildTwo.Should().NotBeNull();

        var inviterGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Inviter Guild"),
            inviter.AccessToken);
        inviterGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var inviterGuild = await inviterGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        inviterGuild.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{inviterGuild!.GuildId}/members/invite",
            new InviteMemberRequest(owner.UserId),
            inviter.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<ListUserGuildsResponse>();
        listPayload.Should().NotBeNull();

        listPayload!.Guilds.Should().HaveCount(3);
        listPayload.Guilds.Should().Contain(guild => guild.GuildId == ownerGuildOne!.GuildId && guild.Role == "Admin");
        listPayload.Guilds.Should().Contain(guild => guild.GuildId == ownerGuildTwo!.GuildId && guild.Role == "Admin");
        listPayload.Guilds.Should().Contain(guild => guild.GuildId == inviterGuild.GuildId && guild.Role == "Member");
    }

    [Fact]
    public async Task GetGuildMembers_WhenRequesterIsMember_ShouldReturnGuildMembers()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Members Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var membersResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members",
            member.AccessToken);
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var membersPayload = await membersResponse.Content.ReadFromJsonAsync<GetGuildMembersResponse>();
        membersPayload.Should().NotBeNull();

        membersPayload!.GuildId.Should().Be(createGuildPayload.GuildId);
        membersPayload.Members.Should().HaveCount(2);
        membersPayload.Members.Should().Contain(x => x.UserId == owner.UserId && x.Role == "Admin");
        membersPayload.Members.Should().Contain(x => x.UserId == member.UserId && x.Role == "Member");
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

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(target.UserId),
            outsider.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();

        error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
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

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            user.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var voiceChannel = channelsPayload!.Channels.First(channel => channel.Type == "Voice");

        var sendMessageResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{voiceChannel.ChannelId}/messages",
            new SendMessageRequest("Should fail"),
            user.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await sendMessageResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();

        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
    }

    [Fact]
    public async Task SendMessage_WhenRateLimitExceeded_ShouldReturnTooManyRequests()
    {
        var user = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Rate Limit Guild"),
            user.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            user.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");

        for (var i = 0; i < 40; i++)
        {
            var sendResponse = await SendAuthorizedPostAsync(
                $"/api/channels/{textChannel.ChannelId}/messages",
                new SendMessageRequest($"msg-{i}"),
                user.AccessToken);

            sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var throttledResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("msg-over-limit"),
            user.AccessToken);

        throttledResponse.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task LeaveGuild_WhenMemberLeaves_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Leave Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/leave",
            member.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task LeaveGuild_WhenOwnerLeaves_ShouldReturn409()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Leave Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/leave",
            owner.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await leaveResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotLeave);
    }

    [Fact]
    public async Task LeaveGuild_WhenNotMember_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Not Member Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/leave",
            outsider.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await leaveResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task LeaveGuild_WhenNotAuthenticated_ShouldReturn401()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Auth Leave Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var leaveResponse = await _client.PostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/leave",
            null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LeaveGuild_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{nonExistentGuildId}/leave",
            user.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await leaveResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
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

    private async Task<HttpResponseMessage> SendAuthorizedPostNoBodyAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
