using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Conversations.DeleteMessage;
using Harmonie.Application.Features.Conversations.EditMessage;
using Harmonie.Application.Features.Conversations.SendMessage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SignalRConversationMessagesHubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SignalRConversationMessagesHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ConversationMessageCreated_WhenParticipantConnected_ShouldReceiveEvent()
    {
        var sender = await AuthTestHelper.RegisterAsync(_client);
        var receiver = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, sender.AccessToken, receiver.UserId);

        await using var connection = CreateHubConnection(receiver.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var messageReceived = new TaskCompletionSource<SignalRConversationMessageCreatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRConversationMessageCreatedEvent>("ConversationMessageCreated", payload =>
        {
            messageReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("hello realtime dm"),
            sender.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
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
    public async Task ConversationMessageUpdated_WhenParticipantConnected_ShouldReceiveEvent()
    {
        var sender = await AuthTestHelper.RegisterAsync(_client);
        var receiver = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, sender.AccessToken, receiver.UserId);

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("original realtime dm"),
            sender.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();

        await using var connection = CreateHubConnection(receiver.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var messageReceived = new TaskCompletionSource<SignalRConversationMessageUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRConversationMessageUpdatedEvent>("ConversationMessageUpdated", payload =>
        {
            messageReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var editResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}/messages/{sendPayload!.MessageId}",
            new EditMessageRequest("updated realtime dm"),
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
    public async Task ConversationMessageDeleted_WhenParticipantConnected_ShouldReceiveEvent()
    {
        var sender = await AuthTestHelper.RegisterAsync(_client);
        var receiver = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, sender.AccessToken, receiver.UserId);

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("delete realtime dm"),
            sender.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();

        await using var connection = CreateHubConnection(receiver.AccessToken);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var messageReceived = new TaskCompletionSource<SignalRConversationMessageDeletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On("Ready", () => ready.TrySetResult());
        connection.On<SignalRConversationMessageDeletedEvent>("ConversationMessageDeleted", payload =>
        {
            messageReceived.TrySetResult(payload);
        });

        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
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

    private sealed record SignalRConversationMessageCreatedEvent(
        string MessageId,
        string ConversationId,
        string AuthorUserId,
        string Content,
        DateTime CreatedAtUtc);

    private sealed record SignalRConversationMessageUpdatedEvent(
        string MessageId,
        string ConversationId,
        string Content,
        DateTime UpdatedAtUtc);

    private sealed record SignalRConversationMessageDeletedEvent(
        string MessageId,
        string ConversationId);
}
