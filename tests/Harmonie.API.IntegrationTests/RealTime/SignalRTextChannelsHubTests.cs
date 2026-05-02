using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Channels.EditMessage;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SignalRTextChannelsHubTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SignalRTextChannelsHubTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MessageCreated_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("SignalR Delivery Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            member.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");
        textChannel.ChannelId.Should().NotBeEmpty();

        await using var connection = CreateHubConnection(member.AccessToken);
        var messageReceived = new TaskCompletionSource<SignalRMessageCreatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRMessageCreatedEvent>("MessageCreated", payload =>
        {
            messageReceived.TrySetResult(payload);
        });

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var sendMessageResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("hello realtime"),
            owner.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendMessagePayload.Should().NotBeNull();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(messageReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(messageReceived.Task);

        var eventPayload = await messageReceived.Task;
        eventPayload.MessageId.Should().Be(sendMessagePayload!.MessageId.ToString());
        eventPayload.ChannelId.Should().Be(textChannel.ChannelId.ToString());
        eventPayload.AuthorUserId.Should().Be(owner.UserId.ToString());
        eventPayload.Content.Should().Be("hello realtime");
    }

    [Fact]
    public async Task MessageUpdated_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("SignalR Update Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            member.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");

        var sendMessageResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("original content"),
            owner.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendMessagePayload.Should().NotBeNull();

        await using var connection = CreateHubConnection(member.AccessToken);
        var eventReceived = new TaskCompletionSource<SignalRMessageUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRMessageUpdatedEvent>("MessageUpdated", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var editResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{textChannel.ChannelId}/messages/{sendMessagePayload!.MessageId}",
            new EditMessageRequest("updated content"),
            owner.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.MessageId.Should().Be(sendMessagePayload.MessageId.ToString());
        eventPayload.ChannelId.Should().Be(textChannel.ChannelId.ToString());
        eventPayload.Content.Should().Be("updated content");
        eventPayload.UpdatedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task MessageDeleted_WhenMemberConnected_ShouldReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("SignalR Delete Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            member.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");

        var sendMessageResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("message to delete"),
            owner.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendMessagePayload.Should().NotBeNull();

        await using var connection = CreateHubConnection(member.AccessToken);
        var eventReceived = new TaskCompletionSource<SignalRMessageDeletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRMessageDeletedEvent>("MessageDeleted", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{textChannel.ChannelId}/messages/{sendMessagePayload!.MessageId}",
            owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.MessageId.Should().Be(sendMessagePayload.MessageId.ToString());
        eventPayload.ChannelId.Should().Be(textChannel.ChannelId.ToString());
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

    private sealed record SignalRMessageCreatedEvent(
        string MessageId,
        string ChannelId,
        string AuthorUserId,
        string AuthorUsername,
        string? AuthorDisplayName,
        string Content,
        DateTime CreatedAtUtc);

    private sealed record SignalRMessageUpdatedEvent(
        string MessageId,
        string ChannelId,
        string Content,
        DateTime UpdatedAtUtc);

    private sealed record SignalRMessageDeletedEvent(
        string MessageId,
        string ChannelId);
}
