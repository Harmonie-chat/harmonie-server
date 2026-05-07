using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class GuildChannelsTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GuildChannelsTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateChannel_WhenAdminCreatesTextChannel_ShouldReturn201()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Channel Text Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            new CreateChannelRequest("announcements", ChannelTypeInput.Text, 2),
            owner.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await createChannelResponse.Content.ReadFromJsonAsync<CreateChannelResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(createGuildPayload.GuildId);
        payload.Name.Should().Be("announcements");
        payload.Type.Should().Be("Text");
        payload.IsDefault.Should().BeFalse();
        payload.Position.Should().Be(2);
        payload.ChannelId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateChannel_WhenAdminCreatesVoiceChannel_ShouldReturn201()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Channel Voice Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            new CreateChannelRequest("Gaming", ChannelTypeInput.Voice, 5),
            owner.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await createChannelResponse.Content.ReadFromJsonAsync<CreateChannelResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Type.Should().Be("Voice");
        payload.Name.Should().Be("Gaming");
    }

    [Fact]
    public async Task CreateChannel_WhenMemberTriesToCreate_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Member Create Channel Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var createChannelResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            new CreateChannelRequest("member-channel", ChannelTypeInput.Text, 3),
            member.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task CreateChannel_WhenNonMemberTriesToCreate_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Create Channel Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            new CreateChannelRequest("outsider-channel", ChannelTypeInput.Text, 3),
            outsider.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task CreateChannel_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentGuildId = Guid.NewGuid();

        var createChannelResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{nonExistentGuildId}/channels",
            new CreateChannelRequest("lost-channel", ChannelTypeInput.Text, 0),
            user.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task CreateChannel_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentGuildId = Guid.NewGuid();

        var createChannelResponse = await _client.PostAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/channels",
            new CreateChannelRequest("anon-channel", ChannelTypeInput.Text, 0),
            TestContext.Current.CancellationToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateChannel_WhenInvalidType_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Invalid Type Channel Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await SendAuthorizedPostRawAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            """{"name":"bad-channel","type":"Video","position":0}""",
            owner.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
        error.Errors.Should().ContainKey("type");
        error.Errors!["type"][0].Code.Should().Be(ApplicationErrorCodes.Validation.WrongEnumValue);
    }

    [Fact]
    public async Task CreateChannel_WhenNegativePosition_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Negative Position Channel Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        var createChannelResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            new CreateChannelRequest("bad-channel", ChannelTypeInput.Text, -1),
            owner.AccessToken);
        createChannelResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await createChannelResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.DomainRuleViolation);
    }

    [Fact]
    public async Task GetGuildChannels_HasUnread_ShouldBeTrueWhenOtherUserSentMessage()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var sender = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"{prefix}-guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, sender.AccessToken);

        var channelsResponse = await _client.SendAuthorizedGetAsync($"/api/guilds/{guildId}/channels", owner.AccessToken);
        var channels = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        var textChannel = channels!.Channels.First(c => c.Type == "Text");

        await ChannelTestHelper.SendChannelMessageAsync(_client, textChannel.ChannelId, "hello", sender.AccessToken);

        var response = await _client.SendAuthorizedGetAsync($"/api/guilds/{guildId}/channels", owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        payload!.Channels.First(c => c.Type == "Text").HasUnread.Should().BeTrue();
        payload.Channels.First(c => c.Type == "Voice").HasUnread.Should().BeFalse();
    }

    [Fact]
    public async Task GetGuildChannels_HasUnread_ShouldBeFalseAfterAcknowledge()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var sender = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"{prefix}-guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, sender.AccessToken);

        var channelsResponse = await _client.SendAuthorizedGetAsync($"/api/guilds/{guildId}/channels", owner.AccessToken);
        var channels = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        var textChannel = channels!.Channels.First(c => c.Type == "Text");

        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, textChannel.ChannelId, "hi", sender.AccessToken);

        var ackResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/ack",
            new { lastReadMessageId = message.MessageId },
            owner.AccessToken);
        ackResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.SendAuthorizedGetAsync($"/api/guilds/{guildId}/channels", owner.AccessToken);
        var payload = await response.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        payload!.Channels.First(c => c.Type == "Text").HasUnread.Should().BeFalse();
    }

    [Fact]
    public async Task GetGuildChannels_HasUnread_ShouldBeFalseForOwnMessages()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"{prefix}-guild");

        var channelsResponse = await _client.SendAuthorizedGetAsync($"/api/guilds/{guildId}/channels", owner.AccessToken);
        var channels = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        var textChannel = channels!.Channels.First(c => c.Type == "Text");

        await ChannelTestHelper.SendChannelMessageAsync(_client, textChannel.ChannelId, "my own message", owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync($"/api/guilds/{guildId}/channels", owner.AccessToken);
        var payload = await response.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        payload!.Channels.First(c => c.Type == "Text").HasUnread.Should().BeFalse();
    }

    private async Task<HttpResponseMessage> SendAuthorizedPostRawAsync(
        string uri,
        string json,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
