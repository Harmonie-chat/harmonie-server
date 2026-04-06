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
}
