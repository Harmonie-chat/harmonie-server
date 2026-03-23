using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.UpdateChannel;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class UpdateChannelTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UpdateChannelTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateChannel_WhenAdminRenamesChannel_ShouldReturn200()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "original-name");

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = "renamed-channel" },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await updateResponse.Content.ReadFromJsonAsync<UpdateChannelResponse>();
        payload.Should().NotBeNull();
        payload!.ChannelId.Should().Be(channelId);
        payload.Name.Should().Be("renamed-channel");
    }

    [Fact]
    public async Task UpdateChannel_WhenAdminUpdatesPosition_ShouldReturn200()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "position-channel", position: 1);

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { position = 10 },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await updateResponse.Content.ReadFromJsonAsync<UpdateChannelResponse>();
        payload.Should().NotBeNull();
        payload!.Position.Should().Be(10);
    }

    [Fact]
    public async Task UpdateChannel_WhenFieldsAreExplicitlyNull_ShouldTreatThemAsNotProvided()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "stable-channel", position: 3);

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = (string?)null, position = (int?)null },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await updateResponse.Content.ReadFromJsonAsync<UpdateChannelResponse>();
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("stable-channel");
        payload.Position.Should().Be(3);
    }

    [Fact]
    public async Task UpdateChannel_WhenMemberTriesToUpdate_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Forbidden Update Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, member.UserId, owner.AccessToken);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "member-test-channel", guildId: guildId);

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = "hacked-name" },
            member.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task UpdateChannel_WhenNonMemberTriesToUpdate_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "outsider-channel");

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = "outsider-rename" },
            outsider.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task UpdateChannel_WhenChannelNotFound_ShouldReturn404()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentChannelId = Guid.NewGuid();

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{nonExistentChannelId}",
            new { name = "ghost-channel" },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task UpdateChannel_WhenNameAlreadyExists_ShouldReturn409()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Name Conflict Guild");

        await CreateChannelInGuildAsync(
            guildId,
            new CreateChannelRequest("taken-name", ChannelTypeInput.Text, 1),
            owner.AccessToken);

        var channelToRenameId = await ChannelTestHelper.CreateChannelAndGetIdAsync(
            _client,
            owner.AccessToken,
            "original-channel",
            guildId: guildId,
            position: 2);

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelToRenameId}",
            new { name = "taken-name" },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NameConflict);
    }

    [Fact]
    public async Task UpdateChannel_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentChannelId = Guid.NewGuid();

        var updateResponse = await _client.PatchAsJsonAsync(
            $"/api/channels/{nonExistentChannelId}",
            new { name = "anon-rename" });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateChannel_WhenNameIsEmpty_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "valid-name");

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = "" },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await updateResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.DomainRuleViolation);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private async Task CreateChannelInGuildAsync(
        string guildId,
        CreateChannelRequest request,
        string accessToken)
    {
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            request,
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
