using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.EditMessage;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.InviteMember;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SignalRTextChannelsHubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SignalRTextChannelsHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task JoinChannel_WhenUserIsNotMember_ShouldReturnAccessDeniedHubException()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("SignalR Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            owner.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");
        var textChannelIdParsed = Guid.TryParse(textChannel.ChannelId, out var textChannelId);
        textChannelIdParsed.Should().BeTrue();

        await using var connection = CreateHubConnection(outsider.AccessToken);
        await connection.StartAsync();

        var act = async () => await connection.InvokeAsync("JoinChannel", textChannelId);

        var exception = await act.Should().ThrowAsync<HubException>();
        exception.Which.Message.Should().Contain(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task MessageCreated_WhenMemberJoinedChannel_ShouldReceiveEvent()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("SignalR Delivery Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            member.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");
        var textChannelIdParsed = Guid.TryParse(textChannel.ChannelId, out var textChannelId);
        textChannelIdParsed.Should().BeTrue();

        await using var connection = CreateHubConnection(member.AccessToken);
        var messageReceived = new TaskCompletionSource<SignalRMessageCreatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRMessageCreatedEvent>("MessageCreated", payload =>
        {
            messageReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await connection.InvokeAsync("JoinChannel", textChannelId);

        var sendMessageResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("hello realtime"),
            owner.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendMessagePayload.Should().NotBeNull();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(messageReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(messageReceived.Task);

        var eventPayload = await messageReceived.Task;
        eventPayload.MessageId.Should().Be(sendMessagePayload!.MessageId);
        eventPayload.ChannelId.Should().Be(textChannel.ChannelId);
        eventPayload.AuthorUserId.Should().Be(owner.UserId);
        eventPayload.Content.Should().Be("hello realtime");
    }

    [Fact]
    public async Task MessageUpdated_WhenMemberJoinedChannel_ShouldReceiveEvent()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("SignalR Update Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            member.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");

        var sendMessageResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("original content"),
            owner.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendMessagePayload.Should().NotBeNull();

        await using var connection = CreateHubConnection(member.AccessToken);
        var eventReceived = new TaskCompletionSource<SignalRMessageUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRMessageUpdatedEvent>("MessageUpdated", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        var textChannelIdParsed = Guid.TryParse(textChannel.ChannelId, out var textChannelId);
        textChannelIdParsed.Should().BeTrue();
        await connection.InvokeAsync("JoinChannel", textChannelId);

        var editResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{textChannel.ChannelId}/messages/{sendMessagePayload!.MessageId}",
            new EditMessageRequest("updated content"),
            owner.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.MessageId.Should().Be(sendMessagePayload.MessageId);
        eventPayload.ChannelId.Should().Be(textChannel.ChannelId);
        eventPayload.Content.Should().Be("updated content");
        eventPayload.UpdatedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task MessageDeleted_WhenMemberJoinedChannel_ShouldReceiveEvent()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("SignalR Delete Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            member.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");

        var sendMessageResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("message to delete"),
            owner.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendMessagePayload.Should().NotBeNull();

        await using var connection = CreateHubConnection(member.AccessToken);
        var eventReceived = new TaskCompletionSource<SignalRMessageDeletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRMessageDeletedEvent>("MessageDeleted", payload =>
        {
            eventReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        var textChannelIdParsed = Guid.TryParse(textChannel.ChannelId, out var textChannelId);
        textChannelIdParsed.Should().BeTrue();
        await connection.InvokeAsync("JoinChannel", textChannelId);

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{textChannel.ChannelId}/messages/{sendMessagePayload!.MessageId}",
            owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(eventReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(eventReceived.Task);

        var eventPayload = await eventReceived.Task;
        eventPayload.MessageId.Should().Be(sendMessagePayload.MessageId);
        eventPayload.ChannelId.Should().Be(textChannel.ChannelId);
    }

    private HubConnection CreateHubConnection(string accessToken)
    {
        var baseAddress = _client.BaseAddress ?? new Uri("http://localhost");
        var hubUri = new Uri(baseAddress, "/hubs/text-channels");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    private async Task<RegisterResponse> RegisterAsync()
    {
        var request = new RegisterRequest(
            Email: $"test{Guid.NewGuid():N}@harmonie.chat",
            Username: $"user{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task<HttpResponseMessage> SendAuthorizedPostAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPatchAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedDeleteAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private sealed record SignalRMessageCreatedEvent(
        string MessageId,
        string ChannelId,
        string AuthorUserId,
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
