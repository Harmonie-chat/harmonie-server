using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateChannel;

namespace Harmonie.API.IntegrationTests.Common;

public static class ChannelTestHelper
{
    public static async Task<(Guid GuildId, Guid ChannelId)> CreateGuildAndChannelAsync(
        HttpClient client,
        string accessToken)
    {
        var guildName = $"guild{Guid.NewGuid():N}"[..16];
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(client, accessToken, guildName);
        var channelId = await CreateChannelAndGetIdAsync(
            client,
            accessToken,
            $"chan{Guid.NewGuid():N}"[..16],
            guildId,
            position: 1);
        return (guildId, channelId);
    }

    public static async Task<SendMessageResponse> SendChannelMessageAsync(
        HttpClient client,
        Guid channelId,
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
        return payload!;
    }


    public static async Task<Guid> CreateChannelAndGetIdAsync(
        HttpClient client,
        string accessToken,
        string name,
        Guid? guildId = null,
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

    public static async Task<Guid> SendMessageAndGetIdAsync(
        HttpClient client,
        Guid channelId,
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

    public static async Task AddReactionAsync(
        HttpClient client,
        Guid channelId,
        Guid messageId,
        string urlEncodedEmoji,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/channels/{channelId}/messages/{messageId}/reactions/{urlEncodedEmoji}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
