using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class DeleteGuildEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DeleteGuildEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteGuild_WhenOwnerDeletesGuild_ShouldReturn204AndCascadeDataRemoval()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Delete Endpoint Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            owner.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");

        var sendMessageResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("guild delete cascade"),
            owner.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var deleteGuildResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            owner.AccessToken);
        deleteGuildResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deletedChannelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            owner.AccessToken);
        deletedChannelsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deletedMessagesResponse = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            owner.AccessToken);
        deletedMessagesResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerGuildsResponse = await _client.SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
        ownerGuildsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ownerGuildsPayload = await ownerGuildsResponse.Content.ReadFromJsonAsync<ListUserGuildsResponse>();
        ownerGuildsPayload.Should().NotBeNull();
        ownerGuildsPayload!.Guilds.Should().NotContain(guild => guild.GuildId == createGuildPayload.GuildId);

        var memberGuildsResponse = await _client.SendAuthorizedGetAsync("/api/guilds", member.AccessToken);
        memberGuildsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var memberGuildsPayload = await memberGuildsResponse.Content.ReadFromJsonAsync<ListUserGuildsResponse>();
        memberGuildsPayload.Should().NotBeNull();
        memberGuildsPayload!.Guilds.Should().NotContain(guild => guild.GuildId == createGuildPayload.GuildId);
    }

    [Fact]
    public async Task DeleteGuild_WhenCallerIsNotOwner_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Delete Forbidden Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var deleteGuildResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            member.AccessToken);
        deleteGuildResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteGuildResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task DeleteGuild_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var deleteGuildResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{Guid.NewGuid()}",
            user.AccessToken);
        deleteGuildResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await deleteGuildResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task DeleteGuild_WhenNotAuthenticated_ShouldReturn401()
    {
        var createGuildResponse = await _client.DeleteAsync($"/api/guilds/{Guid.NewGuid()}");
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
