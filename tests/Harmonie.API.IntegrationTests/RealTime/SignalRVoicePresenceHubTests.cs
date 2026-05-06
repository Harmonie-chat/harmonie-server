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

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

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
        eventPayload.Username.Should().Be(member.Username);
        eventPayload.JoinedAtUtc.Should().NotBe(default);
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

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

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
        eventPayload.Username.Should().Be(member.Username);
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

    // LiveKit proto enum values for TrackSource and TrackType
    private const int LiveKitTrackSourceScreenShare = 3;
    private const int LiveKitTrackTypeVideo = 1;

    private static string CreateTrackWebhookBody(
        string eventType,
        string channelId,
        string userId,
        string participantName,
        string trackSid,
        int trackSource,
        int trackType)
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
              "track": {
                "sid": "{{trackSid}}",
                "source": {{trackSource}},
                "type": {{trackType}},
                "muted": false,
                "width": 1920,
                "height": 1080
              },
              "createdAt": "{{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}}"
            }
            """;

    [Fact]
    public async Task VoiceScreenShareStarted_WhenTrackPublished_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await CreateGuildAndInviteMemberAsync(owner, member);
        var voiceChannelId = await GetVoiceChannelIdAsync(guildId, member.AccessToken);

        await using var connection = CreateHubConnection(member.AccessToken);
        var eventReceived = new TaskCompletionSource<SignalRVoiceScreenShareEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRVoiceScreenShareEvent>("VoiceScreenShareStarted", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var webhookResponse = await SendLiveKitWebhookAsync(
            CreateTrackWebhookBody(
                "track_published",
                voiceChannelId.ToString(),
                member.UserId.ToString(),
                member.Username,
                "TR_screen001",
                trackSource: LiveKitTrackSourceScreenShare,
                trackType: LiveKitTrackTypeVideo));
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(guildId.ToString());
        eventPayload.GuildName.Should().NotBeNullOrEmpty();
        eventPayload.ChannelId.Should().Be(voiceChannelId.ToString());
        eventPayload.ChannelName.Should().NotBeNullOrEmpty();
        eventPayload.UserId.Should().Be(member.UserId.ToString());
        eventPayload.Username.Should().Be(member.Username);
        eventPayload.TimestampUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task VoiceScreenShareStopped_WhenTrackUnpublished_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await CreateGuildAndInviteMemberAsync(owner, member);
        var voiceChannelId = await GetVoiceChannelIdAsync(guildId, member.AccessToken);

        await using var connection = CreateHubConnection(member.AccessToken);

        // First publish a track — register Ready before starting to avoid missing the event
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // HTTP handler processes the webhook synchronously; awaiting the response guarantees the SID is tracked.
        await SendLiveKitWebhookAsync(
            CreateTrackWebhookBody(
                "track_published",
                voiceChannelId.ToString(),
                member.UserId.ToString(),
                member.Username,
                "TR_screen002",
                trackSource: LiveKitTrackSourceScreenShare,
                trackType: LiveKitTrackTypeVideo));

        await connection.DisposeAsync();

        // Reconnect to capture the Stopped event
        await using var connection2 = CreateHubConnection(member.AccessToken);
        var stoppedEvent = new TaskCompletionSource<SignalRVoiceScreenShareEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection2.On<SignalRVoiceScreenShareEvent>("VoiceScreenShareStopped", payload =>
        {
            stoppedEvent.TrySetResult(payload);
        });

        var ready2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection2.On("Ready", () => ready2.TrySetResult());

        await connection2.StartAsync(TestContext.Current.CancellationToken);
        await ready2.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var webhookResponse = await SendLiveKitWebhookAsync(
            CreateTrackWebhookBody(
                "track_unpublished",
                voiceChannelId.ToString(),
                member.UserId.ToString(),
                member.Username,
                "TR_screen002",
                trackSource: LiveKitTrackSourceScreenShare,
                trackType: LiveKitTrackTypeVideo));
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(stoppedEvent.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(stoppedEvent.Task);

        var eventPayload = await stoppedEvent.Task;
        eventPayload.GuildId.Should().Be(guildId.ToString());
        eventPayload.GuildName.Should().NotBeNullOrEmpty();
        eventPayload.ChannelId.Should().Be(voiceChannelId.ToString());
        eventPayload.ChannelName.Should().NotBeNullOrEmpty();
        eventPayload.UserId.Should().Be(member.UserId.ToString());
        eventPayload.Username.Should().Be(member.Username);
        eventPayload.TimestampUtc.Should().NotBe(default);
    }

    private static string CreateLiveKitWebhookToken(string rawBody)
    {
        var checksum = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
        return new Livekit.Server.Sdk.Dotnet.AccessToken(LiveKitApiKey, LiveKitApiSecret)
            .WithSha256(checksum)
            .ToJwt();
    }

    private sealed record SignalRVoiceParticipantJoinedEvent(
        string GuildId,
        string GuildName,
        string ChannelId,
        string ChannelName,
        string UserId,
        string? Username,
        string? DisplayName,
        Guid? AvatarFileId,
        string? AvatarColor,
        string? AvatarIcon,
        string? AvatarBg,
        DateTime JoinedAtUtc);

    private sealed record SignalRVoiceParticipantLeftEvent(
        string GuildId,
        string GuildName,
        string ChannelId,
        string ChannelName,
        string UserId,
        string? Username,
        DateTime LeftAtUtc);

    private sealed record SignalRVoiceScreenShareEvent(
        string GuildId,
        string GuildName,
        string ChannelId,
        string ChannelName,
        string UserId,
        string? Username,
        DateTime TimestampUtc);
}
