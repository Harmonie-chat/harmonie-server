using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class CreateChannelTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CreateChannelTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateChannel_WhenTypeIsMissing_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Missing Type Guild");

        var createResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            new { name = "missing-type", position = 0 },
            owner.AccessToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await createResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task CreateChannel_WhenNameAlreadyExists_ShouldReturn409()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Create Channel Name Conflict Guild");

        await CreateChannelInGuildAsync(
            guildId,
            new CreateChannelRequest("taken-name", ChannelTypeInput.Text, 1),
            owner.AccessToken);

        var createResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            new CreateChannelRequest("taken-name", ChannelTypeInput.Text, 2),
            owner.AccessToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await createResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NameConflict);
    }

    private async Task CreateChannelInGuildAsync(
        Guid guildId,
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
