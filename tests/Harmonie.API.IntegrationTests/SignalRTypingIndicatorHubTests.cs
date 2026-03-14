using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Conversations.OpenConversation;
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

public sealed class SignalRTypingIndicatorHubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SignalRTypingIndicatorHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Guild text channel typing ─────────────────────────────────

    [Fact]
    public async Task StartTypingChannel_WhenUserIsNotMember_ShouldReturnAccessDeniedHubException()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Typing AccessDenied Guild"),
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

        var act = async () => await connection.InvokeAsync("StartTypingChannel", textChannelId);

        var exception = await act.Should().ThrowAsync<HubException>();
        exception.Which.Message.Should().Contain(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task StartTypingChannel_WhenMemberTypesInChannel_ShouldBroadcastToOtherMembers()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Typing Broadcast Guild"),
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

        await using var receiverConnection = CreateHubConnection(member.AccessToken);
        var typingReceived = new TaskCompletionSource<SignalRUserTypingEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        receiverConnection.On<SignalRUserTypingEvent>("UserTyping", payload =>
        {
            typingReceived.TrySetResult(payload);
        });

        await receiverConnection.StartAsync();
        await receiverConnection.InvokeAsync("JoinChannel", textChannelId);

        await using var senderConnection = CreateHubConnection(owner.AccessToken);
        await senderConnection.StartAsync();
        await senderConnection.InvokeAsync("JoinChannel", textChannelId);
        await senderConnection.InvokeAsync("StartTypingChannel", textChannelId);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(typingReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(typingReceived.Task);

        var eventPayload = await typingReceived.Task;
        eventPayload.UserId.Should().Be(owner.UserId);
        eventPayload.ChannelId.Should().Be(textChannel.ChannelId);
        eventPayload.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartTypingChannel_WhenCalledWithinThrottleWindow_ShouldNotBroadcastSecondEvent()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Typing Throttle Guild"),
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

        var eventsReceived = 0;
        await using var receiverConnection = CreateHubConnection(member.AccessToken);
        receiverConnection.On<SignalRUserTypingEvent>("UserTyping", _ =>
        {
            Interlocked.Increment(ref eventsReceived);
        });

        await receiverConnection.StartAsync();
        await receiverConnection.InvokeAsync("JoinChannel", textChannelId);

        await using var senderConnection = CreateHubConnection(owner.AccessToken);
        await senderConnection.StartAsync();
        await senderConnection.InvokeAsync("JoinChannel", textChannelId);

        await senderConnection.InvokeAsync("StartTypingChannel", textChannelId);
        await senderConnection.InvokeAsync("StartTypingChannel", textChannelId);
        await senderConnection.InvokeAsync("StartTypingChannel", textChannelId);

        await Task.Delay(TimeSpan.FromSeconds(1));

        eventsReceived.Should().Be(1);
    }

    // ── Conversation typing ───────────────────────────────────────

    [Fact]
    public async Task StartTypingConversation_WhenUserIsNotParticipant_ShouldReturnAccessDeniedHubException()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);

        await using var connection = CreateHubConnection(outsider.AccessToken);
        await connection.StartAsync();

        var act = async () => await connection.InvokeAsync("StartTypingConversation", Guid.Parse(conversationId));

        var exception = await act.Should().ThrowAsync<HubException>();
        exception.Which.Message.Should().Contain(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task StartTypingConversation_WhenParticipantTypes_ShouldBroadcastToOtherParticipant()
    {
        var sender = await RegisterAsync();
        var receiver = await RegisterAsync();
        var conversationId = await OpenConversationAsync(sender.AccessToken, receiver.UserId);

        await using var receiverConnection = CreateHubConnection(receiver.AccessToken);
        var typingReceived = new TaskCompletionSource<SignalRConversationUserTypingEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        receiverConnection.On<SignalRConversationUserTypingEvent>("ConversationUserTyping", payload =>
        {
            typingReceived.TrySetResult(payload);
        });

        await receiverConnection.StartAsync();
        await receiverConnection.InvokeAsync("JoinConversation", Guid.Parse(conversationId));

        await using var senderConnection = CreateHubConnection(sender.AccessToken);
        await senderConnection.StartAsync();
        await senderConnection.InvokeAsync("JoinConversation", Guid.Parse(conversationId));
        await senderConnection.InvokeAsync("StartTypingConversation", Guid.Parse(conversationId));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(typingReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(typingReceived.Task);

        var eventPayload = await typingReceived.Task;
        eventPayload.UserId.Should().Be(sender.UserId);
        eventPayload.ConversationId.Should().Be(conversationId);
        eventPayload.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartTypingConversation_WhenCalledWithinThrottleWindow_ShouldNotBroadcastSecondEvent()
    {
        var sender = await RegisterAsync();
        var receiver = await RegisterAsync();
        var conversationId = await OpenConversationAsync(sender.AccessToken, receiver.UserId);

        var eventsReceived = 0;
        await using var receiverConnection = CreateHubConnection(receiver.AccessToken);
        receiverConnection.On<SignalRConversationUserTypingEvent>("ConversationUserTyping", _ =>
        {
            Interlocked.Increment(ref eventsReceived);
        });

        await receiverConnection.StartAsync();
        await receiverConnection.InvokeAsync("JoinConversation", Guid.Parse(conversationId));

        await using var senderConnection = CreateHubConnection(sender.AccessToken);
        await senderConnection.StartAsync();
        await senderConnection.InvokeAsync("JoinConversation", Guid.Parse(conversationId));

        await senderConnection.InvokeAsync("StartTypingConversation", Guid.Parse(conversationId));
        await senderConnection.InvokeAsync("StartTypingConversation", Guid.Parse(conversationId));
        await senderConnection.InvokeAsync("StartTypingConversation", Guid.Parse(conversationId));

        await Task.Delay(TimeSpan.FromSeconds(1));

        eventsReceived.Should().Be(1);
    }

    // ── Helpers ───────────────────────────────────────────────────

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

    private async Task<string> OpenConversationAsync(string accessToken, string targetUserId)
    {
        var response = await SendAuthorizedPostAsync(
            "/api/conversations",
            new OpenConversationRequest(targetUserId),
            accessToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<OpenConversationResponse>();
        payload.Should().NotBeNull();
        return payload!.ConversationId;
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

    private sealed record SignalRUserTypingEvent(
        string UserId,
        string ChannelId,
        DateTime Timestamp);

    private sealed record SignalRConversationUserTypingEvent(
        string UserId,
        string ConversationId,
        DateTime Timestamp);
}
