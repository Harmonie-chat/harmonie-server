using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateChannel;

namespace Harmonie.API.IntegrationTests.Common;

public static class ChannelTestHelper
{
    public static async Task<string> CreateChannelAndGetIdAsync(
        HttpClient client,
        string accessToken,
        string name,
        string? guildId = null,
        int position = 0)
    {
        if (guildId is null)
        {
            guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(client, accessToken, $"Guild for {name}");
        }

        var response = await client.SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            new CreateChannelRequest(name, ChannelTypeInput.Text, position),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateChannelResponse>();
        payload.Should().NotBeNull();

        return payload!.ChannelId;
    }

    public static async Task<string> SendMessageAndGetIdAsync(
        HttpClient client,
        string channelId,
        string content,
        string accessToken)
    {
        var response = await client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();

        return payload!.MessageId;
    }
}
