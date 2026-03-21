using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.BanMember;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Channels.GetMessages;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class BanMemberEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BanMemberEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task BanMember_WhenAdminBansMember_ShouldReturn201AndRemoveFromGuild()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"BanGuild{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);

        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId, "Spamming"),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var banPayload = await banResponse.Content.ReadFromJsonAsync<BanMemberResponse>();
        banPayload.Should().NotBeNull();
        banPayload!.GuildId.Should().Be(guild.GuildId);
        banPayload.UserId.Should().Be(member.UserId);
        banPayload.Reason.Should().Be("Spamming");
        banPayload.BannedBy.Should().Be(owner.UserId);

        // Verify member was removed from guild
        var membersResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/members",
            owner.AccessToken);
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var membersPayload = await membersResponse.Content.ReadFromJsonAsync<GetGuildMembersResponse>();
        membersPayload.Should().NotBeNull();
        membersPayload!.Members.Should().NotContain(m => m.UserId == member.UserId);
    }

    [Fact]
    public async Task BanMember_WhenBannedUserTriesAcceptInvite_ShouldReturn403()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var banned = await AuthTestHelper.RegisterAsync(_client, token + "b");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"BanInvite{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, banned.UserId, owner.AccessToken);

        // Ban the member
        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(banned.UserId),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Create a new invite link
        var invite = await GuildTestHelper.CreateInviteAsync(_client, guild.GuildId, owner.AccessToken);

        // Banned user tries to accept
        var acceptResponse = await _client.SendAuthorizedPostNoBodyAsync(
            $"/api/invites/{invite.Code}/accept",
            banned.AccessToken);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await acceptResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.UserBanned);
    }

    [Fact]
    public async Task BanMember_WhenNonAdmin_ShouldReturn403()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");
        var target = await AuthTestHelper.RegisterAsync(_client, token + "t");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"NonAdmBan{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);
        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, target.UserId, owner.AccessToken);

        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(target.UserId),
            member.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await banResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task BanMember_WhenBanOwner_ShouldReturn409()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"BanOwner{token}", owner.AccessToken);

        // Owner tries to ban themselves (owner check comes before self-ban check in handler)
        // Let's use a second admin to try to ban the owner
        var admin = await AuthTestHelper.RegisterAsync(_client, token + "a");
        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, admin.UserId, owner.AccessToken);

        // Promote to admin
        await _client.SendAuthorizedPutAsync(
            $"/api/guilds/{guild.GuildId}/members/{admin.UserId}/role",
            new { Role = "Admin" },
            owner.AccessToken);

        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(owner.UserId),
            admin.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await banResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotBeBanned);
    }

    [Fact]
    public async Task BanMember_WhenAlreadyBanned_ShouldReturn409()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"DblBan{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);

        var firstBan = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId),
            owner.AccessToken);
        firstBan.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondBan = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId),
            owner.AccessToken);
        secondBan.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await secondBan.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AlreadyBanned);
    }

    [Fact]
    public async Task BanMember_WithPurge_ShouldSoftDeleteMessages()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var member = await AuthTestHelper.RegisterAsync(_client, token + "m");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"PurgeBan{token}", owner.AccessToken);

        await GuildTestHelper.InviteMemberAsync(_client, guild.GuildId, member.UserId, owner.AccessToken);

        // Get text channel
        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{guild.GuildId}/channels",
            owner.AccessToken);
        var channels = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        var textChannel = channels!.Channels.First(c => c.Type == "Text");

        // Member sends a message
        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest($"Hello from {token}"),
            member.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Ban with purge
        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild.GuildId}/bans",
            new BanMemberRequest(member.UserId, PurgeMessagesDays: 7),
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify messages are soft-deleted (owner should see no messages from banned user)
        var messagesResponse = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            owner.AccessToken);
        messagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var messages = await messagesResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        messages.Should().NotBeNull();
        messages!.Items.Should().NotContain(m => m.AuthorUserId == member.UserId);
    }

    [Fact]
    public async Task BanMember_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentGuildId = Guid.NewGuid();

        var banResponse = await _client.PostAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/bans",
            new BanMemberRequest(Guid.NewGuid().ToString()));
        banResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
