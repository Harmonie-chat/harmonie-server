using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Interfaces.Messages;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SignalRMessageEventsSafetyNetTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private const string ThumbsUpEmoji = "👍";
    private const string ThumbsUpEmojiEncoded = "%F0%9F%91%8D";

    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SignalRMessageEventsSafetyNetTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ReactionAdded_WhenChannelMemberConnected_ShouldReceiveEvent()
    {
        var (owner, member, channelId) = await CreateChannelScenarioAsync();
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "react channel", owner.AccessToken);

        await using var connection = CreateHubConnection(_factory, member.AccessToken);
        var received = new TaskCompletionSource<SignalRReactionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SignalRReactionEvent>("ReactionAdded", payload => received.TrySetResult(payload));
        await StartAndWaitReadyAsync(connection);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/{ThumbsUpEmojiEncoded}",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var payload = await WaitForAsync(received);
        payload.MessageId.Should().Be(message.MessageId);
        payload.ChannelId.Should().Be(channelId);
        payload.ConversationId.Should().BeNull();
        payload.UserId.Should().Be(owner.UserId);
        payload.Emoji.Should().Be(ThumbsUpEmoji);
    }

    [Fact]
    public async Task ReactionRemoved_WhenChannelMemberConnected_ShouldReceiveEvent()
    {
        var (owner, member, channelId) = await CreateChannelScenarioAsync();
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "unreact channel", owner.AccessToken);
        await ChannelTestHelper.AddReactionAsync(_client, channelId, message.MessageId, ThumbsUpEmojiEncoded, owner.AccessToken);

        await using var connection = CreateHubConnection(_factory, member.AccessToken);
        var received = new TaskCompletionSource<SignalRReactionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SignalRReactionEvent>("ReactionRemoved", payload => received.TrySetResult(payload));
        await StartAndWaitReadyAsync(connection);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/reactions/{ThumbsUpEmojiEncoded}",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var payload = await WaitForAsync(received);
        payload.MessageId.Should().Be(message.MessageId);
        payload.ChannelId.Should().Be(channelId);
        payload.ConversationId.Should().BeNull();
        payload.UserId.Should().Be(owner.UserId);
        payload.Emoji.Should().Be(ThumbsUpEmoji);
    }

    [Fact]
    public async Task MessagePinned_WhenChannelMemberConnected_ShouldReceiveEvent()
    {
        var (owner, member, channelId) = await CreateChannelScenarioAsync();
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "pin channel", owner.AccessToken);

        await using var connection = CreateHubConnection(_factory, member.AccessToken);
        var received = new TaskCompletionSource<SignalRMessagePinnedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SignalRMessagePinnedEvent>("MessagePinned", payload => received.TrySetResult(payload));
        await StartAndWaitReadyAsync(connection);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/pin",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var payload = await WaitForAsync(received);
        payload.MessageId.Should().Be(message.MessageId);
        payload.ChannelId.Should().Be(channelId);
        payload.ConversationId.Should().BeNull();
        payload.PinnedByUserId.Should().Be(owner.UserId);
        payload.PinnedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task MessageUnpinned_WhenChannelMemberConnected_ShouldReceiveEvent()
    {
        var (owner, member, channelId) = await CreateChannelScenarioAsync();
        var message = await ChannelTestHelper.SendChannelMessageAsync(_client, channelId, "unpin channel", owner.AccessToken);
        var pinResponse = await SendAuthorizedPutNoBodyAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/pin",
            owner.AccessToken);
        pinResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var connection = CreateHubConnection(_factory, member.AccessToken);
        var received = new TaskCompletionSource<SignalRMessageUnpinnedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SignalRMessageUnpinnedEvent>("MessageUnpinned", payload => received.TrySetResult(payload));
        await StartAndWaitReadyAsync(connection);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{message.MessageId}/pin",
            owner.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var payload = await WaitForAsync(received);
        payload.MessageId.Should().Be(message.MessageId);
        payload.ChannelId.Should().Be(channelId);
        payload.ConversationId.Should().BeNull();
        payload.UnpinnedByUserId.Should().Be(owner.UserId);
        payload.UnpinnedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ReactionAdded_WhenConversationParticipantConnected_ShouldReceiveEvent()
    {
        var (sender, receiver, conversationId) = await CreateConversationScenarioAsync();
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "react conversation", sender.AccessToken);

        await using var connection = CreateHubConnection(_factory, receiver.AccessToken);
        var received = new TaskCompletionSource<SignalRReactionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SignalRReactionEvent>("ReactionAdded", payload => received.TrySetResult(payload));
        await StartAndWaitReadyAsync(connection);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/{ThumbsUpEmojiEncoded}",
            sender.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var payload = await WaitForAsync(received);
        payload.MessageId.Should().Be(message.MessageId);
        payload.ChannelId.Should().BeNull();
        payload.ConversationId.Should().Be(conversationId);
        payload.UserId.Should().Be(sender.UserId);
        payload.Emoji.Should().Be(ThumbsUpEmoji);
    }

    [Fact]
    public async Task ReactionRemoved_WhenConversationParticipantConnected_ShouldReceiveEvent()
    {
        var (sender, receiver, conversationId) = await CreateConversationScenarioAsync();
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "unreact conversation", sender.AccessToken);
        await ConversationTestHelper.AddReactionAsync(_client, conversationId, message.MessageId, ThumbsUpEmojiEncoded, sender.AccessToken);

        await using var connection = CreateHubConnection(_factory, receiver.AccessToken);
        var received = new TaskCompletionSource<SignalRReactionEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SignalRReactionEvent>("ReactionRemoved", payload => received.TrySetResult(payload));
        await StartAndWaitReadyAsync(connection);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/reactions/{ThumbsUpEmojiEncoded}",
            sender.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var payload = await WaitForAsync(received);
        payload.MessageId.Should().Be(message.MessageId);
        payload.ChannelId.Should().BeNull();
        payload.ConversationId.Should().Be(conversationId);
        payload.UserId.Should().Be(sender.UserId);
        payload.Emoji.Should().Be(ThumbsUpEmoji);
    }

    [Fact]
    public async Task MessagePinned_WhenConversationParticipantConnected_ShouldReceiveEvent()
    {
        var (sender, receiver, conversationId) = await CreateConversationScenarioAsync();
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "pin conversation", sender.AccessToken);

        await using var connection = CreateHubConnection(_factory, receiver.AccessToken);
        var received = new TaskCompletionSource<SignalRMessagePinnedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SignalRMessagePinnedEvent>("MessagePinned", payload => received.TrySetResult(payload));
        await StartAndWaitReadyAsync(connection);

        var response = await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/pin",
            sender.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var payload = await WaitForAsync(received);
        payload.MessageId.Should().Be(message.MessageId);
        payload.ChannelId.Should().BeNull();
        payload.ConversationId.Should().Be(conversationId);
        payload.PinnedByUserId.Should().Be(sender.UserId);
        payload.PinnedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task MessageUnpinned_WhenConversationParticipantConnected_ShouldReceiveEvent()
    {
        var (sender, receiver, conversationId) = await CreateConversationScenarioAsync();
        var message = await ConversationTestHelper.SendConversationMessageAsync(_client, conversationId, "unpin conversation", sender.AccessToken);
        var pinResponse = await SendAuthorizedPutNoBodyAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/pin",
            sender.AccessToken);
        pinResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var connection = CreateHubConnection(_factory, receiver.AccessToken);
        var received = new TaskCompletionSource<SignalRMessageUnpinnedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SignalRMessageUnpinnedEvent>("MessageUnpinned", payload => received.TrySetResult(payload));
        await StartAndWaitReadyAsync(connection);

        var response = await SendAuthorizedDeleteAsync(
            $"/api/conversations/{conversationId}/messages/{message.MessageId}/pin",
            sender.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var payload = await WaitForAsync(received);
        payload.MessageId.Should().Be(message.MessageId);
        payload.ChannelId.Should().BeNull();
        payload.ConversationId.Should().Be(conversationId);
        payload.UnpinnedByUserId.Should().Be(sender.UserId);
        payload.UnpinnedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task MessagePreviewUpdated_WhenConversationMessageContainsUrl_ShouldReceiveEvent()
    {
        var testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILinkPreviewFetcher>();
                services.AddScoped<ILinkPreviewFetcher>(_ => new FakeLinkPreviewFetcher());
            });
        });
        var testClient = testFactory.CreateClient();
        var sender = await AuthTestHelper.RegisterAsync(testClient);
        var receiver = await AuthTestHelper.RegisterAsync(testClient);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(testClient, sender.AccessToken, receiver.UserId);

        await using var connection = CreateHubConnection(testFactory, receiver.AccessToken);
        var received = new TaskCompletionSource<SignalRMessagePreviewUpdatedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<SignalRMessagePreviewUpdatedEvent>("MessagePreviewUpdated", payload => received.TrySetResult(payload));
        await StartAndWaitReadyAsync(connection);

        var sendResponse = await testClient.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("Check this https://example.com/conversation"),
            sender.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendPayload.Should().NotBeNull();

        var payload = await WaitForAsync(received, TimeSpan.FromSeconds(15));
        payload.MessageId.Should().Be(sendPayload!.MessageId);
        payload.ChannelId.Should().BeNull();
        payload.ConversationId.Should().Be(conversationId);
        payload.Previews.Should().ContainSingle();
        payload.Previews[0].Url.Should().Be("https://example.com/conversation");
        payload.Previews[0].Title.Should().Be("Example Title");
    }

    private async Task<(RegisterResponse Owner, RegisterResponse Member, Guid ChannelId)> CreateChannelScenarioAsync()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest($"Message Events {Guid.NewGuid():N}"[..28]),
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
        return (owner, member, textChannel.ChannelId);
    }

    private async Task<(RegisterResponse Sender, RegisterResponse Receiver, Guid ConversationId)> CreateConversationScenarioAsync()
    {
        var sender = await AuthTestHelper.RegisterAsync(_client);
        var receiver = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, sender.AccessToken, receiver.UserId);
        return (sender, receiver, conversationId);
    }

    private static HubConnection CreateHubConnection(WebApplicationFactory<Program> factory, string accessToken)
    {
        var hubUri = new Uri(factory.Server.BaseAddress, "/hubs/realtime");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();
    }

    private static async Task StartAndWaitReadyAsync(HubConnection connection)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static async Task<T> WaitForAsync<T>(TaskCompletionSource<T> source, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        var completed = await Task.WhenAny(source.Task, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token));
        completed.Should().Be(source.Task);
        return await source.Task;
    }

    private async Task<HttpResponseMessage> SendAuthorizedPutNoBodyAsync(string url, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthorizedDeleteAsync(string url, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed record SignalRReactionEvent(
        Guid MessageId,
        Guid? ChannelId,
        Guid? ConversationId,
        Guid UserId,
        string Emoji);

    private sealed record SignalRMessagePinnedEvent(
        Guid MessageId,
        Guid? ChannelId,
        Guid? ConversationId,
        Guid PinnedByUserId,
        DateTime PinnedAtUtc);

    private sealed record SignalRMessageUnpinnedEvent(
        Guid MessageId,
        Guid? ChannelId,
        Guid? ConversationId,
        Guid UnpinnedByUserId,
        DateTime UnpinnedAtUtc);

    private sealed record SignalRMessagePreviewUpdatedEvent(
        Guid MessageId,
        Guid? ChannelId,
        Guid? ConversationId,
        IReadOnlyList<SignalRLinkPreviewDto> Previews);

    private sealed record SignalRLinkPreviewDto(
        string Url,
        string? Title,
        string? Description,
        string? ImageUrl,
        string? SiteName);

    private sealed class FakeLinkPreviewFetcher : ILinkPreviewFetcher
    {
        public Task<LinkPreviewMetadata?> FetchAsync(Uri url, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<LinkPreviewMetadata?>(new LinkPreviewMetadata(
                url.ToString(),
                "Example Title",
                "Example Description",
                null,
                "Example Site"));
        }
    }
}
