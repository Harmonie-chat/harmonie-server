using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.TransferOwnership;
using Harmonie.Application.Features.Guilds.UpdateGuild;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class GuildEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

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
        createGuildPayload!.IconUrl.Should().BeNull();
        createGuildPayload.Icon.Should().BeNull();

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
        listPayload.Guilds.Should().OnlyContain(guild => guild.IconUrl == null && guild.Icon == null);
    }

    [Fact]
    public async Task UpdateGuild_WithOwnerAndPartialIconUpdate_ShouldPersistAndKeepOmittedSubFields()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Patchable Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var seedResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}",
            new
            {
                icon = new { color = "#7C3AED", name = "sword", bg = "#1F2937" },
                iconUrl = "https://cdn.harmonie.chat/guild-icon.png"
            },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new
            {
                name = "Renamed Guild",
                icon = new { color = "#F59E0B" }
            },
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateGuildResponse>();
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(createGuildPayload.GuildId);
        payload.Name.Should().Be("Renamed Guild");
        payload.IconUrl.Should().Be("https://cdn.harmonie.chat/guild-icon.png");
        payload.Icon.Should().NotBeNull();
        payload.Icon!.Color.Should().Be("#F59E0B");
        payload.Icon.Name.Should().Be("sword");
        payload.Icon.Bg.Should().Be("#1F2937");

        var listResponse = await SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<ListUserGuildsResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Guilds.Should().Contain(guild =>
            guild.GuildId == createGuildPayload.GuildId
            && guild.Name == "Renamed Guild"
            && guild.IconUrl == "https://cdn.harmonie.chat/guild-icon.png"
            && guild.Icon != null
            && guild.Icon.Color == "#F59E0B"
            && guild.Icon.Name == "sword"
            && guild.Icon.Bg == "#1F2937");
    }

    [Fact]
    public async Task UpdateGuild_WithAdminAndNullIcon_ShouldClearIconFields()
    {
        var owner = await RegisterAsync();
        var admin = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Admin Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(admin.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var promoteResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{admin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);
        promoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var seedResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new
            {
                iconUrl = "https://cdn.harmonie.chat/admin-guild.png",
                icon = new { color = "#7C3AED", name = "sword", bg = "#1F2937" }
            },
            owner.AccessToken);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new
            {
                iconUrl = (string?)null,
                icon = (object?)null
            },
            admin.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateGuildResponse>();
        payload.Should().NotBeNull();
        payload!.IconUrl.Should().BeNull();
        payload.Icon.Should().BeNull();
    }

    [Fact]
    public async Task CreateGuild_WithIconFields_ShouldPersistIconData()
    {
        var owner = await RegisterAsync();

        var createResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new
            {
                name = "Guild With Icon",
                iconUrl = "https://cdn.harmonie.chat/guild-icon.png",
                icon = new { color = "#7C3AED", name = "sword", bg = "#1F2937" }
            },
            owner.AccessToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createPayload.Should().NotBeNull();
        createPayload!.IconUrl.Should().Be("https://cdn.harmonie.chat/guild-icon.png");
        createPayload.Icon.Should().NotBeNull();
        createPayload.Icon!.Color.Should().Be("#7C3AED");
        createPayload.Icon.Name.Should().Be("sword");
        createPayload.Icon.Bg.Should().Be("#1F2937");

        var listResponse = await SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<ListUserGuildsResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Guilds.Should().Contain(guild =>
            guild.GuildId == createPayload.GuildId
            && guild.IconUrl == "https://cdn.harmonie.chat/guild-icon.png"
            && guild.Icon != null
            && guild.Icon.Color == "#7C3AED"
            && guild.Icon.Name == "sword"
            && guild.Icon.Bg == "#1F2937");
    }

    [Fact]
    public async Task CreateGuild_WithPartialIconFields_ShouldPersistProvidedFields()
    {
        var owner = await RegisterAsync();

        var createResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new
            {
                name = "Partial Icon Guild",
                icon = new { color = "#F59E0B" }
            },
            owner.AccessToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createPayload.Should().NotBeNull();
        createPayload!.IconUrl.Should().BeNull();
        createPayload.Icon.Should().NotBeNull();
        createPayload.Icon!.Color.Should().Be("#F59E0B");
        createPayload.Icon.Name.Should().BeNull();
        createPayload.Icon.Bg.Should().BeNull();
    }

    [Fact]
    public async Task UpdateGuild_WhenCallerIsRegularMember_ShouldReturnForbidden()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Forbidden Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            new { name = "Should Fail" },
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
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
    public async Task InviteMember_WhenTargetUserDoesNotExist_ShouldReturnNotFound()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Missing Invite Target Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(Guid.NewGuid().ToString()),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteTargetNotFound);
    }

    [Fact]
    public async Task InviteMember_WhenTargetUserIsAlreadyMember_ShouldReturnConflict()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Duplicate Invite Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var firstInviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        firstInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondInviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        secondInviteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await secondInviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberAlreadyExists);
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
            Content = JsonContent.Create(payload, options: _jsonOptions)
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

    private async Task<HttpResponseMessage> SendAuthorizedPostRawAsync(
        string uri,
        string json,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
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

    private async Task<HttpResponseMessage> SendAuthorizedPatchAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPutAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPatchRawAsync(
        string uri,
        string json,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPutRawAsync(
        string uri,
        string json,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task TransferOwnership_WhenOwnerTransfersToMember_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/owner/transfer",
            new TransferOwnershipRequest(member.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferOwnership_WhenNonOwnerTriesToTransfer_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var otherMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Owner Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(otherMember.UserId),
            owner.AccessToken);

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/owner/transfer",
            new TransferOwnershipRequest(otherMember.UserId),
            member.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task TransferOwnership_WhenOwnerTransfersToSelf_ShouldReturn409()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Self Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/owner/transfer",
            new TransferOwnershipRequest(owner.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerTransferToSelf);
    }

    [Fact]
    public async Task TransferOwnership_WhenNewOwnerIsNotMember_ShouldReturn404()
    {
        var owner = await RegisterAsync();
        var nonMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/owner/transfer",
            new TransferOwnershipRequest(nonMember.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
    }

    [Fact]
    public async Task TransferOwnership_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var target = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{nonExistentGuildId}/owner/transfer",
            new TransferOwnershipRequest(target.UserId),
            user.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task TransferOwnership_WhenNotAuthenticated_ShouldReturn401()
    {
        var target = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var transferResponse = await _client.PostAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/owner/transfer",
            new TransferOwnershipRequest(target.UserId));
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveMember_WhenAdminRemovesMember_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Remove Member Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveMember_WhenNonAdminTriesToRemove_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var otherMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Admin Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(otherMember.UserId),
            owner.AccessToken);

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{otherMember.UserId}",
            member.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task RemoveMember_WhenNotAuthenticated_ShouldReturn401()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Unauthenticated Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await _client.DeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{member.UserId}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveMember_WhenAdminTriesToRemoveOwner_ShouldReturn409()
    {
        // The owner is the only Admin in a newly created guild.
        // When the owner (admin) tries to remove themselves, the endpoint
        // must reject with 409 because the owner cannot be removed.
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{owner.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotBeRemoved);
    }

    [Fact]
    public async Task RemoveMember_WhenAdminTriesToRemoveNonMember_ShouldReturn404()
    {
        var owner = await RegisterAsync();
        var nonMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{nonMember.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenAdminPromotesMember_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Promote Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRoleResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenAdminDemotesAdmin_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var otherAdmin = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Demote Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(otherAdmin.UserId),
            owner.AccessToken);

        await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{otherAdmin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);

        var demoteResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{otherAdmin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Member),
            owner.AccessToken);
        demoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenNonAdminTriesToChangeRole_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var target = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Admin Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(target.UserId),
            owner.AccessToken);

        var updateRoleResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{target.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            member.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenAdminTriesToChangeOwnerRole_ShouldReturn409()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var updateRoleResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{owner.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Member),
            owner.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerRoleCannotBeChanged);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var updateRoleResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{nonExistentGuildId}/members/{targetId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            user.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentGuildId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var updateRoleResponse = await _client.PutAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/members/{targetId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin));
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenInvalidRole_ShouldReturn400()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Invalid Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        var updateRoleResponse = await SendAuthorizedPutRawAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}/role",
            """{"role":"Owner"}""",
            owner.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task CreateChannel_WhenAdminCreatesTextChannel_ShouldReturn201()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Channel Text Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            new CreateChannelRequest("announcements", ChannelTypeInput.Text, 2),
            owner.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await createChannelResponse.Content.ReadFromJsonAsync<CreateChannelResponse>();
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(createGuildPayload.GuildId);
        payload.Name.Should().Be("announcements");
        payload.Type.Should().Be("Text");
        payload.IsDefault.Should().BeFalse();
        payload.Position.Should().Be(2);
        payload.ChannelId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateChannel_WhenAdminCreatesVoiceChannel_ShouldReturn201()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Channel Voice Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            new CreateChannelRequest("Gaming", ChannelTypeInput.Voice, 5),
            owner.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await createChannelResponse.Content.ReadFromJsonAsync<CreateChannelResponse>();
        payload.Should().NotBeNull();
        payload!.Type.Should().Be("Voice");
        payload.Name.Should().Be("Gaming");
    }

    [Fact]
    public async Task CreateChannel_WhenMemberTriesToCreate_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Member Create Channel Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        var createChannelResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            new CreateChannelRequest("member-channel", ChannelTypeInput.Text, 3),
            member.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task CreateChannel_WhenNonMemberTriesToCreate_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Create Channel Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            new CreateChannelRequest("outsider-channel", ChannelTypeInput.Text, 3),
            outsider.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task CreateChannel_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var createChannelResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{nonExistentGuildId}/channels",
            new CreateChannelRequest("lost-channel", ChannelTypeInput.Text, 0),
            user.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task CreateChannel_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentGuildId = Guid.NewGuid();

        var createChannelResponse = await _client.PostAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/channels",
            new CreateChannelRequest("anon-channel", ChannelTypeInput.Text, 0));
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateChannel_WhenInvalidType_ShouldReturn400()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Invalid Type Channel Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await SendAuthorizedPostRawAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            """{"name":"bad-channel","type":"Video","position":0}""",
            owner.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task CreateChannel_WhenNegativePosition_ShouldReturn400()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Negative Position Channel Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            new CreateChannelRequest("bad-channel", ChannelTypeInput.Text, -1),
            owner.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }
}
