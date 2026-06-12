using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

/// <summary>
/// Race-condition safety nets: concurrent requests hitting the same unique
/// resource must produce exactly one success and clean expected errors for
/// the losers, never an HTTP 500 or duplicate/inconsistent data.
/// </summary>
public sealed class ConcurrentCreationRaceTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConcurrentCreationRaceTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ConcurrentRequestsWithSameIdentity_ShouldCreateSingleUser()
    {
        var token = Guid.NewGuid().ToString("N")[..12];
        var request = new RegisterRequest(
            Email: $"race{token}@harmonie.chat",
            Username: $"race{token}",
            Password: "Test123!@#");

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => _client.PostAsJsonAsync("/api/auth/register", request)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);

        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            loser.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var error = await loser.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
            error.Should().NotBeNull();
            error!.Code.Should().BeOneOf(
                ApplicationErrorCodes.Auth.DuplicateEmail,
                ApplicationErrorCodes.Auth.DuplicateUsername);
        }
    }

    [Fact]
    public async Task AcceptInvite_ConcurrentAcceptsOnLastUse_ShouldNotExceedMaxUses()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client, token);
        var joinerA = await AuthTestHelper.RegisterAsync(_client, token + "a");
        var joinerB = await AuthTestHelper.RegisterAsync(_client, token + "b");

        var guild = await GuildTestHelper.CreateGuildAsync(_client, $"RaceGuild{token}", owner.AccessToken);
        var invite = await GuildTestHelper.CreateInviteAsync(_client, guild.GuildId, owner.AccessToken, maxUses: 1);

        var responses = await Task.WhenAll(
            _client.SendAuthorizedPostNoBodyAsync($"/api/invites/{invite.Code}/accept", joinerA.AccessToken),
            _client.SendAuthorizedPostNoBodyAsync($"/api/invites/{invite.Code}/accept", joinerB.AccessToken));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);

        var loser = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
        loser.StatusCode.Should().Be(HttpStatusCode.Gone);

        var error = await loser.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Invite.Exhausted);
    }

    [Fact]
    public async Task CreateChannel_ConcurrentRequestsWithSameName_ShouldCreateSingleChannel()
    {
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
}
