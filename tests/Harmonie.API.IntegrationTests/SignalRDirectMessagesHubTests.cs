using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Conversations.DeleteDirectMessage;
using Harmonie.Application.Features.Conversations.EditDirectMessage;
using Harmonie.Application.Features.Conversations.OpenConversation;
using Harmonie.Application.Features.Conversations.SendDirectMessage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SignalRDirectMessagesHubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SignalRDirectMessagesHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task JoinConversation_WhenUserIsNotParticipant_ShouldReturnAccessDeniedHubException()
    {
        var participantOne = await RegisterAsync();
        var participantTwo = await RegisterAsync();
        var outsider = await RegisterAsync();
        var conversationId = await OpenConversationAsync(participantOne.AccessToken, participantTwo.UserId);

        await using var connection = CreateHubConnection(outsider.AccessToken);
        await connection.StartAsync();

        var act = async () => await connection.InvokeAsync("JoinConversation", Guid.Parse(conversationId));

        var exception = await act.Should().ThrowAsync<HubException>();
        exception.Which.Message.Should().Contain(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task DirectMessageCreated_WhenParticipantJoinedConversation_ShouldReceiveEvent()
    {
        var sender = await RegisterAsync();
        var receiver = await RegisterAsync();
        var conversationId = await OpenConversationAsync(sender.AccessToken, receiver.UserId);

        await using var connection = CreateHubConnection(receiver.AccessToken);
        var messageReceived = new TaskCompletionSource<SignalRDirectMessageCreatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRDirectMessageCreatedEvent>("DirectMessageCreated", payload =>
        {
            messageReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await connection.InvokeAsync("JoinConversation", Guid.Parse(conversationId));

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendDirectMessageRequest("hello realtime dm"),
            sender.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendDirectMessageResponse>();
        sendPayload.Should().NotBeNull();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(messageReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(messageReceived.Task);

        var eventPayload = await messageReceived.Task;
        eventPayload.MessageId.Should().Be(sendPayload!.MessageId);
        eventPayload.ConversationId.Should().Be(conversationId);
        eventPayload.AuthorUserId.Should().Be(sender.UserId);
        eventPayload.Content.Should().Be("hello realtime dm");
    }

    [Fact]
    public async Task DirectMessageUpdated_WhenParticipantJoinedConversation_ShouldReceiveEvent()
    {
        var sender = await RegisterAsync();
        var receiver = await RegisterAsync();
        var conversationId = await OpenConversationAsync(sender.AccessToken, receiver.UserId);

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendDirectMessageRequest("original realtime dm"),
            sender.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendDirectMessageResponse>();
        sendPayload.Should().NotBeNull();

        await using var connection = CreateHubConnection(receiver.AccessToken);
        var messageReceived = new TaskCompletionSource<SignalRDirectMessageUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRDirectMessageUpdatedEvent>("DirectMessageUpdated", payload =>
        {
            messageReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await connection.InvokeAsync("JoinConversation", Guid.Parse(conversationId));

        var editResponse = await SendAuthorizedPutAsync(
            $"/api/conversations/{conversationId}/messages/{sendPayload!.MessageId}",
            new EditDirectMessageRequest("updated realtime dm"),
            sender.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(messageReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(messageReceived.Task);

        var eventPayload = await messageReceived.Task;
        eventPayload.MessageId.Should().Be(sendPayload.MessageId);
        eventPayload.ConversationId.Should().Be(conversationId);
        eventPayload.Content.Should().Be("updated realtime dm");
        eventPayload.UpdatedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task DirectMessageDeleted_WhenParticipantJoinedConversation_ShouldReceiveEvent()
    {
        var sender = await RegisterAsync();
        var receiver = await RegisterAsync();
        var conversationId = await OpenConversationAsync(sender.AccessToken, receiver.UserId);

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendDirectMessageRequest("delete realtime dm"),
            sender.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendDirectMessageResponse>();
        sendPayload.Should().NotBeNull();

        await using var connection = CreateHubConnection(receiver.AccessToken);
        var messageReceived = new TaskCompletionSource<SignalRDirectMessageDeletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRDirectMessageDeletedEvent>("DirectMessageDeleted", payload =>
        {
            messageReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await connection.InvokeAsync("JoinConversation", Guid.Parse(conversationId));

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{sendPayload!.MessageId}",
            sender.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(messageReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(messageReceived.Task);

        var eventPayload = await messageReceived.Task;
        eventPayload.MessageId.Should().Be(sendPayload.MessageId);
        eventPayload.ConversationId.Should().Be(conversationId);
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

    private async Task<HttpResponseMessage> SendAuthorizedPutAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
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

    private sealed record SignalRDirectMessageCreatedEvent(
        string MessageId,
        string ConversationId,
        string AuthorUserId,
        string Content,
        DateTime CreatedAtUtc);

    private sealed record SignalRDirectMessageUpdatedEvent(
        string MessageId,
        string ConversationId,
        string Content,
        DateTime UpdatedAtUtc);

    private sealed record SignalRDirectMessageDeletedEvent(
        string MessageId,
        string ConversationId);
}
