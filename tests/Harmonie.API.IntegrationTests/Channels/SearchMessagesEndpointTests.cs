using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.SearchMessages;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SearchMessagesEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SearchMessagesEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchMessages_WhenCallerIsGuildMember_ShouldReturnMatchesWithContext()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Search Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, member.AccessToken);

        var generalChannelId = await GetDefaultTextChannelIdAsync(owner.AccessToken, guildId);
        var deploymentsChannelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "deployments", guildId, 10);

        await SendMessageAsync(generalChannelId, "deploy alpha", owner.AccessToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await SendMessageAsync(generalChannelId, "random chatter", owner.AccessToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await SendMessageAsync(deploymentsChannelId, "deploy beta", member.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            BuildSearchUri(guildId, "deploy"),
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SearchMessagesResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(guildId);
        payload.Items.Should().HaveCount(2);
        payload.Items.Select(item => item.Content).Should().Equal("deploy beta", "deploy alpha");
        payload.Items.Select(item => item.ChannelName).Should().Equal("deployments", "general");
        payload.Items[0].AuthorUsername.Should().Be(member.Username);
        payload.Items[1].AuthorUsername.Should().Be(owner.Username);
    }

    [Fact]
    public async Task SearchMessages_WithFiltersAndCursor_ShouldReturnNextPage()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Search Pagination Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, member.AccessToken);

        var deploymentsChannelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "deployments", guildId, 10);

        await SendMessageAsync(deploymentsChannelId, "incident one", owner.AccessToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await SendMessageAsync(deploymentsChannelId, "incident two", member.AccessToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await SendMessageAsync(deploymentsChannelId, "incident three", owner.AccessToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await SendMessageAsync(deploymentsChannelId, "incident four", owner.AccessToken);

        var firstResponse = await _client.SendAuthorizedGetAsync(
            BuildSearchUri(
                guildId,
                "incident",
                channelId: deploymentsChannelId,
                authorId: owner.UserId,
                limit: 2),
            owner.AccessToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<SearchMessagesResponse>(TestContext.Current.CancellationToken);
        firstPayload.Should().NotBeNull();
        firstPayload!.Items.Select(item => item.Content).Should().Equal("incident four", "incident three");
        firstPayload.NextCursor.Should().NotBeNullOrWhiteSpace();

        var secondResponse = await _client.SendAuthorizedGetAsync(
            BuildSearchUri(
                guildId,
                "incident",
                channelId: deploymentsChannelId,
                authorId: owner.UserId,
                limit: 2,
                cursor: firstPayload.NextCursor),
            owner.AccessToken);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<SearchMessagesResponse>(TestContext.Current.CancellationToken);
        secondPayload.Should().NotBeNull();
        secondPayload!.Items.Select(item => item.Content).Should().Equal("incident one");
        secondPayload.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task SearchMessages_WhenCallerIsNotGuildMember_ShouldReturnForbidden()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Forbidden Search Guild");

        var response = await _client.SendAuthorizedGetAsync(
            BuildSearchUri(guildId, "deploy"),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    private async Task<Guid> GetDefaultTextChannelIdAsync(string accessToken, Guid guildId)
    {
        var response = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();

        return payload!.Channels.First(channel => channel.Type == "Text").ChannelId;
    }

    private async Task SendMessageAsync(Guid channelId, string content, string accessToken)
    {
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static string BuildSearchUri(
        Guid guildId,
        string query,
        Guid? channelId = null,
        Guid? authorId = null,
        int? limit = null,
        string? cursor = null)
    {
        var parts = new List<string>
        {
            $"q={Uri.EscapeDataString(query)}"
        };

        if (channelId is not null)
            parts.Add($"channelId={Uri.EscapeDataString(channelId.Value.ToString())}");

        if (authorId is not null)
            parts.Add($"authorId={Uri.EscapeDataString(authorId.Value.ToString())}");

        if (limit.HasValue)
            parts.Add($"limit={limit.Value}");

        if (!string.IsNullOrWhiteSpace(cursor))
            parts.Add($"cursor={Uri.EscapeDataString(cursor)}");

        return $"/api/guilds/{guildId}/messages/search?{string.Join("&", parts)}";
    }
}
