using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SignalRVoicePresenceHubTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private const string LiveKitApiKey = "devkey";
    private const string LiveKitApiSecret = "devsecret-that-is-long-enough-for-hmac-signing";

    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SignalRVoicePresenceHubTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task VoiceParticipantJoined_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await CreateGuildAndInviteMemberAsync(owner, member);
        var voiceChannelId = await GetVoiceChannelIdAsync(guildId, member.AccessToken);

        await using var connection = CreateHubConnection(member.AccessToken);
        var eventReceived = new TaskCompletionSource<SignalRVoiceParticipantJoinedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRVoiceParticipantJoinedEvent>("VoiceParticipantJoined", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var webhookResponse = await SendLiveKitWebhookAsync(
            CreateParticipantWebhookBody("participant_joined", voiceChannelId.ToString(), member.UserId.ToString(), member.Username));
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(guildId.ToString());
        eventPayload.ChannelId.Should().Be(voiceChannelId.ToString());
        eventPayload.UserId.Should().Be(member.UserId.ToString());
        eventPayload.ParticipantName.Should().Be(member.Username);
        eventPayload.JoinedAtUtc.Should().NotBe(default);
        // Avatar fields originate from the user profile fetched during webhook processing.
        // A freshly registered user has no avatar set, so all fields are null.
        eventPayload.DisplayName.Should().BeNull();
        eventPayload.AvatarFileId.Should().BeNull();
        eventPayload.AvatarColor.Should().BeNull();
        eventPayload.AvatarIcon.Should().BeNull();
        eventPayload.AvatarBg.Should().BeNull();
    }

    [Fact]
    public async Task VoiceParticipantLeft_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await CreateGuildAndInviteMemberAsync(owner, member);
        var voiceChannelId = await GetVoiceChannelIdAsync(guildId, member.AccessToken);

        await using var connection = CreateHubConnection(member.AccessToken);
        var eventReceived = new TaskCompletionSource<SignalRVoiceParticipantLeftEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRVoiceParticipantLeftEvent>("VoiceParticipantLeft", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var webhookResponse = await SendLiveKitWebhookAsync(
            CreateParticipantWebhookBody("participant_left", voiceChannelId.ToString(), member.UserId.ToString(), member.Username));
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(guildId.ToString());
        eventPayload.ChannelId.Should().Be(voiceChannelId.ToString());
        eventPayload.UserId.Should().Be(member.UserId.ToString());
        eventPayload.ParticipantName.Should().Be(member.Username);
        eventPayload.LeftAtUtc.Should().NotBe(default);
    }

    private HubConnection CreateHubConnection(string accessToken)
    {
        var baseAddress = _client.BaseAddress ?? new Uri("http://localhost");
        var hubUri = new Uri(baseAddress, "/hubs/realtime");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    private async Task<Guid> CreateGuildAndInviteMemberAsync(RegisterResponse owner, RegisterResponse member)
    {
        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Voice Presence Delivery Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        return createGuildPayload.GuildId;
    }

    private async Task<Guid> GetVoiceChannelIdAsync(Guid guildId, string accessToken)
    {
        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            accessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var voiceChannel = channelsPayload!.Channels.First(channel => channel.Type == "Voice");
        return voiceChannel.ChannelId;
    }

    private async Task<HttpResponseMessage> SendLiveKitWebhookAsync(string rawBody)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/livekit")
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/webhook+json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateLiveKitWebhookToken(rawBody));

        return await _client.SendAsync(request);
    }

    private static string CreateParticipantWebhookBody(
        string eventType,
        string channelId,
        string userId,
        string participantName)
        => $$"""
            {
              "event": "{{eventType}}",
              "room": {
                "name": "channel:{{channelId}}"
              },
              "participant": {
                "identity": "{{userId}}",
                "name": "{{participantName}}"
              },
              "createdAt": "{{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}"
            }
            """;

    private static string CreateLiveKitWebhookToken(string rawBody)
    {
        var checksum = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
        return new Livekit.Server.Sdk.Dotnet.AccessToken(LiveKitApiKey, LiveKitApiSecret)
            .WithSha256(checksum)
            .ToJwt();
    }

    private sealed record SignalRVoiceParticipantJoinedEvent(
        string GuildId,
        string ChannelId,
        string UserId,
        string ParticipantName,
        string? DisplayName,
        Guid? AvatarFileId,
        string? AvatarColor,
        string? AvatarIcon,
        string? AvatarBg,
        DateTime JoinedAtUtc);

    private sealed record SignalRVoiceParticipantLeftEvent(
        string GuildId,
        string ChannelId,
        string UserId,
        string ParticipantName,
        DateTime LeftAtUtc);
}
