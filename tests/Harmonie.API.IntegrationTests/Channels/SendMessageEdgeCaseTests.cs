using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SendMessageEdgeCaseTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SendMessageEdgeCaseTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendMessage_WhenChannelIsVoice_ShouldReturnConflict()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Voice Guild"),
            user.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            user.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var voiceChannel = channelsPayload!.Channels.First(channel => channel.Type == "Voice");

        var sendMessageResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{voiceChannel.ChannelId}/messages",
            new SendMessageRequest("Should fail"),
            user.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await sendMessageResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();

        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
    }

    [Fact]
    public async Task SendMessage_WhenRateLimitExceeded_ShouldReturnTooManyRequests()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Rate Limit Guild"),
            user.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            user.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");

        for (var i = 0; i < 40; i++)
        {
            var sendResponse = await _client.SendAuthorizedPostAsync(
                $"/api/channels/{textChannel.ChannelId}/messages",
                new SendMessageRequest($"msg-{i}"),
                user.AccessToken);

            sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var throttledResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("msg-over-limit"),
            user.AccessToken);

        throttledResponse.StatusCode.Should().Be((HttpStatusCode)429);
    }
}
