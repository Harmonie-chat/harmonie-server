using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class DeleteChannelTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DeleteChannelTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteChannel_WhenAdminDeletesChannel_ShouldReturn204()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "channel-to-delete");

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}",
            owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteChannel_WhenMemberTriesToDelete_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Forbidden Delete Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, member.UserId, owner.AccessToken);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "member-delete-channel", guildId: guildId);

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}",
            member.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task DeleteChannel_WhenNonMemberTriesToDelete_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "outsider-delete-channel");

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}",
            outsider.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task DeleteChannel_WhenChannelNotFound_ShouldReturn404()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentChannelId = Guid.NewGuid();

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{nonExistentChannelId}",
            owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task DeleteChannel_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentChannelId = Guid.NewGuid();

        var deleteResponse = await _client.DeleteAsync($"/api/channels/{nonExistentChannelId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteChannel_WhenDefaultChannel_ShouldReturn409()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Default Channel Delete Guild");

        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            owner.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var defaultChannel = channelsPayload!.Channels.First(c => c.IsDefault);

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{defaultChannel.ChannelId}",
            owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.CannotDeleteDefault);
    }
}
