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

        var error = await createResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
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

        var error = await createResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NameConflict);
    }

    [Fact]
    public async Task CreateChannel_ConcurrentRequestsWithSameName_ShouldCreateSingleChannel()
    {
        // Race safety net: the losers must get a clean name conflict, never a 500
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"ChanRace{token}");

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(i => _client.SendAuthorizedPostAsync(
                $"/api/guilds/{guildId}/channels",
                new CreateChannelRequest($"race-channel-{token}", ChannelTypeInput.Text, 10 + i),
                owner.AccessToken)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);

        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            loser.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var error = await loser.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
            error.Should().NotBeNull();
            error!.Code.Should().Be(ApplicationErrorCodes.Channel.NameConflict);
        }
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
