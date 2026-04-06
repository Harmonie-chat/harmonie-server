using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SignalRGuildHubTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SignalRGuildHubTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GuildDeleted_WhenMemberConnected_ShouldReceiveEventBeforeDeletion()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("SignalR Guild Delete"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        await using var connection = CreateHubConnection(member.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventReceived = new TaskCompletionSource<SignalRGuildDeletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRGuildDeletedEvent>("GuildDeleted", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var deleteGuildResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}",
            owner.AccessToken);
        deleteGuildResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(createGuildPayload.GuildId.ToString());
    }

    [Fact]
    public async Task ChannelUpdated_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var prefix = Guid.NewGuid().ToString("N")[..8];

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"SignalR ChannelUpdated Guild {prefix}"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(
            _client,
            owner.AccessToken,
            $"original-{prefix}",
            guildId: createGuildPayload.GuildId,
            position: 1);

        await using var connection = CreateHubConnection(member.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventReceived = new TaskCompletionSource<SignalRChannelUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRChannelUpdatedEvent>("ChannelUpdated", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}",
            new { name = $"renamed-{prefix}" },
            owner.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(createGuildPayload.GuildId.ToString());
        eventPayload.ChannelId.Should().Be(channelId.ToString());
        eventPayload.Name.Should().Be($"renamed-{prefix}");
    }

    [Fact]
    public async Task ChannelDeleted_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var prefix = Guid.NewGuid().ToString("N")[..8];

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"SignalR ChannelDeleted Guild {prefix}"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(
            _client,
            owner.AccessToken,
            $"to-delete-{prefix}",
            guildId: createGuildPayload.GuildId,
            position: 1);

        await using var connection = CreateHubConnection(member.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventReceived = new TaskCompletionSource<SignalRChannelDeletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRChannelDeletedEvent>("ChannelDeleted", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}",
            owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(createGuildPayload.GuildId.ToString());
        eventPayload.ChannelId.Should().Be(channelId.ToString());
    }

    [Fact]
    public async Task ChannelsReordered_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var prefix = Guid.NewGuid().ToString("N")[..8];

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"SignalR ChannelsReordered Guild {prefix}"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var ch1 = await ChannelTestHelper.CreateChannelAndGetIdAsync(
            _client,
            owner.AccessToken,
            $"reorder-a-{prefix}",
            guildId: createGuildPayload.GuildId,
            position: 0);

        var ch2 = await ChannelTestHelper.CreateChannelAndGetIdAsync(
            _client,
            owner.AccessToken,
            $"reorder-b-{prefix}",
            guildId: createGuildPayload.GuildId,
            position: 1);

        await using var connection = CreateHubConnection(member.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventReceived = new TaskCompletionSource<SignalRChannelsReorderedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRChannelsReorderedEvent>("ChannelsReordered", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var reorderResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels/reorder",
            new { channels = new[] { new { channelId = ch2, position = 0 }, new { channelId = ch1, position = 1 } } },
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(createGuildPayload.GuildId.ToString());
        eventPayload.Channels.Should().NotBeEmpty();

        var reorderedCh1 = eventPayload.Channels.First(c => c.ChannelId == ch1.ToString());
        var reorderedCh2 = eventPayload.Channels.First(c => c.ChannelId == ch2.ToString());
        reorderedCh1.Position.Should().Be(1);
        reorderedCh2.Position.Should().Be(0);
    }

    [Fact]
    public async Task MemberJoined_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var existingMember = await AuthTestHelper.RegisterAsync(_client);
        var joiningMember = await AuthTestHelper.RegisterAsync(_client);

        var prefix = Guid.NewGuid().ToString("N")[..8];

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"SignalR MemberJoined Guild {prefix}"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, existingMember.AccessToken);

        await using var connection = CreateHubConnection(existingMember.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventReceived = new TaskCompletionSource<SignalRMemberJoinedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRMemberJoinedEvent>("MemberJoined", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload.GuildId, owner.AccessToken, joiningMember.AccessToken);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(createGuildPayload.GuildId.ToString());
        eventPayload.UserId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MemberLeft_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var remainingMember = await AuthTestHelper.RegisterAsync(_client);
        var leavingMember = await AuthTestHelper.RegisterAsync(_client);

        var prefix = Guid.NewGuid().ToString("N")[..8];

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"SignalR MemberLeft Guild {prefix}"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, remainingMember.AccessToken);
        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload.GuildId, owner.AccessToken, leavingMember.AccessToken);

        await using var connection = CreateHubConnection(remainingMember.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventReceived = new TaskCompletionSource<SignalRMemberLeftEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRMemberLeftEvent>("MemberLeft", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var leaveResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/leave",
            new { },
            leavingMember.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(createGuildPayload.GuildId.ToString());
        eventPayload.UserId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MemberBanned_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var remainingMember = await AuthTestHelper.RegisterAsync(_client);
        var targetMember = await AuthTestHelper.RegisterAsync(_client);

        var prefix = Guid.NewGuid().ToString("N")[..8];

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"SignalR MemberBanned Guild {prefix}"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, remainingMember.AccessToken);
        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload.GuildId, owner.AccessToken, targetMember.AccessToken);

        await using var connection = CreateHubConnection(remainingMember.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventReceived = new TaskCompletionSource<SignalRMemberBannedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRMemberBannedEvent>("MemberBanned", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var banResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/bans",
            new { userId = targetMember.UserId, reason = (string?)null, purgeMessagesDays = 0 },
            owner.AccessToken);
        banResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.GuildId.Should().Be(createGuildPayload.GuildId.ToString());
        eventPayload.UserId.Should().Be(targetMember.UserId.ToString());
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

    private sealed record SignalRGuildDeletedEvent(string GuildId);

    private sealed record SignalRChannelUpdatedEvent(
        string GuildId,
        string ChannelId,
        string Name,
        int Position);

    private sealed record SignalRChannelDeletedEvent(
        string GuildId,
        string ChannelId);

    private sealed record SignalRChannelsReorderedEvent(
        string GuildId,
        SignalRChannelPositionItem[] Channels);

    private sealed record SignalRChannelPositionItem(
        string ChannelId,
        int Position);

    private sealed record SignalRMemberJoinedEvent(
        string GuildId,
        string UserId,
        string? DisplayName,
        string? AvatarFileId);

    private sealed record SignalRMemberLeftEvent(
        string GuildId,
        string UserId);

    private sealed record SignalRMemberBannedEvent(
        string GuildId,
        string UserId);
}
