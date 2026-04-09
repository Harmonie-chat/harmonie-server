using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SignalRUserProfileHubTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SignalRUserProfileHubTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UserProfileUpdated_WhenMemberUpdatesDisplayName_GuildMemberShouldReceiveEvent()
    {
        var updatingUser = await AuthTestHelper.RegisterAsync(_client);
        var observer = await AuthTestHelper.RegisterAsync(_client);

        var prefix = Guid.NewGuid().ToString("N")[..8];

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Profile Update Guild {prefix}"),
            updatingUser.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, updatingUser.AccessToken, observer.AccessToken);

        await using var connection = CreateHubConnection(observer.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventReceived = new TaskCompletionSource<SignalRUserProfileUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRUserProfileUpdatedEvent>("UserProfileUpdated", payload =>
        {
            if (payload.UserId == updatingUser.UserId.ToString())
                eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { displayName = $"Updated-{prefix}" },
            updatingUser.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.UserId.Should().Be(updatingUser.UserId.ToString());
        eventPayload.DisplayName.Should().Be($"Updated-{prefix}");
    }

    [Fact]
    public async Task UserProfileUpdated_WhenThemeOnlyChanges_ShouldNotSendEvent()
    {
        var updatingUser = await AuthTestHelper.RegisterAsync(_client);
        var observer = await AuthTestHelper.RegisterAsync(_client);

        var prefix = Guid.NewGuid().ToString("N")[..8];

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Theme Only Guild {prefix}"),
            updatingUser.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, updatingUser.AccessToken, observer.AccessToken);

        await using var connection = CreateHubConnection(observer.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eventReceived = new TaskCompletionSource<SignalRUserProfileUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRUserProfileUpdatedEvent>("UserProfileUpdated", payload =>
        {
            if (payload.UserId == updatingUser.UserId.ToString())
                eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var updateResponse = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { theme = "dark" },
            updatingUser.AccessToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().NotBe(eventReceived.Task, "theme-only changes should not trigger UserProfileUpdated");
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

    private sealed record SignalRUserProfileUpdatedEvent(
        string UserId,
        string? DisplayName,
        string? AvatarFileId);
}
