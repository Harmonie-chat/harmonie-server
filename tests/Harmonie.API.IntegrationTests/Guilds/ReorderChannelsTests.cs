using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.ReorderChannels;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ReorderChannelsTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ReorderChannelsTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ReorderChannels_WhenAdminReordersChannels_ShouldReturn200WithNewPositions()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Reorder Guild");

        var ch1 = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "reorder-ch1", guildId, position: 10);
        var ch2 = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "reorder-ch2", guildId, position: 11);

        var reorderResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch2, 0),
                new ReorderChannelsItemRequest(ch1, 1)
            ]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await reorderResponse.Content.ReadFromJsonAsync<ReorderChannelsResponse>();
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(guildId);

        var reorderedCh1 = payload.Channels.First(c => c.ChannelId == ch1);
        var reorderedCh2 = payload.Channels.First(c => c.ChannelId == ch2);
        reorderedCh1.Position.Should().Be(1);
        reorderedCh2.Position.Should().Be(0);
    }

    [Fact]
    public async Task ReorderChannels_WhenVerifiedByGet_ShouldPersistNewOrder()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Reorder Persist Guild");

        var ch1 = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "persist-ch1", guildId, position: 10);
        var ch2 = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "persist-ch2", guildId, position: 11);

        await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch2, 0),
                new ReorderChannelsItemRequest(ch1, 1)
            ]),
            owner.AccessToken);

        var getResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            owner.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channels = await getResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channels.Should().NotBeNull();

        var getCh1 = channels!.Channels.First(c => c.ChannelId == ch1);
        var getCh2 = channels.Channels.First(c => c.ChannelId == ch2);
        getCh1.Position.Should().Be(1);
        getCh2.Position.Should().Be(0);
    }

    [Fact]
    public async Task ReorderChannels_WhenNonAdminAttempts_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Reorder NonAdmin Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, member.AccessToken);

        var ch1 = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "noadmin-ch1", guildId, position: 0);

        var reorderResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch1, 5)
            ]),
            member.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReorderChannels_WhenChannelNotInGuild_ShouldReturn404()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Reorder NotFound Guild");

        var reorderResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(Guid.NewGuid(), 0)
            ]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReorderChannels_WhenEmptyList_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Reorder Empty Guild");

        var reorderResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReorderChannels_WhenNotAuthenticated_ShouldReturn401()
    {
        var response = await _client.PatchAsync(
            $"/api/guilds/{Guid.NewGuid()}/channels/reorder",
            JsonContent.Create(new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(Guid.NewGuid(), 0)
            ])));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReorderChannels_WhenDuplicateChannelId_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Reorder Dup Guild");
        var ch1 = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "dup-ch1", guildId, position: 0);

        var reorderResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch1, 0),
                new ReorderChannelsItemRequest(ch1, 1)
            ]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
