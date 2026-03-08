using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.SearchMessages;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SearchMessagesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public SearchMessagesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchMessages_WhenCallerIsGuildMember_ShouldReturnMatchesWithContext()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Search Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var generalChannelId = await GetDefaultTextChannelIdAsync(owner.AccessToken, guildId);
        var deploymentsChannelId = await CreateChannelAndGetIdAsync(owner.AccessToken, guildId, "deployments");

        await SendMessageAsync(generalChannelId, "deploy alpha", owner.AccessToken);
        await Task.Delay(20);
        await SendMessageAsync(generalChannelId, "random chatter", owner.AccessToken);
        await Task.Delay(20);
        await SendMessageAsync(deploymentsChannelId, "deploy beta", member.AccessToken);

        var response = await SendAuthorizedGetAsync(
            BuildSearchUri(guildId, "deploy"),
            member.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SearchMessagesResponse>();
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(guildId);
        payload.Items.Should().HaveCount(2);
        payload.Items.Select(item => item.Content).Should().Equal("deploy alpha", "deploy beta");
        payload.Items.Select(item => item.ChannelName).Should().Equal("general", "deployments");
        payload.Items[0].AuthorUsername.Should().Be(owner.Username);
        payload.Items[1].AuthorUsername.Should().Be(member.Username);
    }

    [Fact]
    public async Task SearchMessages_WithFiltersAndCursor_ShouldReturnNextPage()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Search Pagination Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var deploymentsChannelId = await CreateChannelAndGetIdAsync(owner.AccessToken, guildId, "deployments");

        await SendMessageAsync(deploymentsChannelId, "incident one", owner.AccessToken);
        await Task.Delay(20);
        await SendMessageAsync(deploymentsChannelId, "incident two", member.AccessToken);
        await Task.Delay(20);
        await SendMessageAsync(deploymentsChannelId, "incident three", owner.AccessToken);
        await Task.Delay(20);
        await SendMessageAsync(deploymentsChannelId, "incident four", owner.AccessToken);

        var firstResponse = await SendAuthorizedGetAsync(
            BuildSearchUri(
                guildId,
                "incident",
                channelId: deploymentsChannelId,
                authorId: owner.UserId,
                limit: 2),
            owner.AccessToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<SearchMessagesResponse>();
        firstPayload.Should().NotBeNull();
        firstPayload!.Items.Select(item => item.Content).Should().Equal("incident three", "incident four");
        firstPayload.NextCursor.Should().NotBeNullOrWhiteSpace();

        var secondResponse = await SendAuthorizedGetAsync(
            BuildSearchUri(
                guildId,
                "incident",
                channelId: deploymentsChannelId,
                authorId: owner.UserId,
                limit: 2,
                cursor: firstPayload.NextCursor),
            owner.AccessToken);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<SearchMessagesResponse>();
        secondPayload.Should().NotBeNull();
        secondPayload!.Items.Select(item => item.Content).Should().Equal("incident one");
        secondPayload.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task SearchMessages_WhenCallerIsNotGuildMember_ShouldReturnForbidden()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Forbidden Search Guild");

        var response = await SendAuthorizedGetAsync(
            BuildSearchUri(guildId, "deploy"),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
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

    private async Task<string> CreateGuildAndGetIdAsync(string accessToken, string guildName)
    {
        var response = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(guildName),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateGuildResponse>();
        payload.Should().NotBeNull();

        return payload!.GuildId;
    }

    private async Task InviteMemberAsync(string guildId, string userId, string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/members/invite",
            new InviteMemberRequest(userId),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> GetDefaultTextChannelIdAsync(string accessToken, string guildId)
    {
        var response = await SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        payload.Should().NotBeNull();

        return payload!.Channels.First(channel => channel.Type == "Text").ChannelId;
    }

    private async Task<string> CreateChannelAndGetIdAsync(string accessToken, string guildId, string name)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            new CreateChannelRequest(name, ChannelTypeInput.Text, 10),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateChannelResponse>();
        payload.Should().NotBeNull();

        return payload!.ChannelId;
    }

    private async Task SendMessageAsync(string channelId, string content, string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static string BuildSearchUri(
        string guildId,
        string query,
        string? channelId = null,
        string? authorId = null,
        int? limit = null,
        string? cursor = null)
    {
        var parts = new List<string>
        {
            $"q={Uri.EscapeDataString(query)}"
        };

        if (channelId is not null)
            parts.Add($"channelId={Uri.EscapeDataString(channelId)}");

        if (authorId is not null)
            parts.Add($"authorId={Uri.EscapeDataString(authorId)}");

        if (limit.HasValue)
            parts.Add($"limit={limit.Value}");

        if (!string.IsNullOrWhiteSpace(cursor))
            parts.Add($"cursor={Uri.EscapeDataString(cursor)}");

        return $"/api/guilds/{guildId}/messages/search?{string.Join("&", parts)}";
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

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(string uri, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
