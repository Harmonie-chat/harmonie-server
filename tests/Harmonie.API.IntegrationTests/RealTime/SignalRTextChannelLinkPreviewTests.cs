using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
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

public sealed class SignalRTextChannelLinkPreviewTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SignalRTextChannelLinkPreviewTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MessagePreviewUpdated_WhenMessageContainsUrl_ShouldReceiveEvent()
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

        var owner = await AuthTestHelper.RegisterAsync(testClient);
        var member = await AuthTestHelper.RegisterAsync(testClient);

        var createGuildResponse = await testClient.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Link Preview Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>(TestContext.Current.CancellationToken);
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(testClient, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var channelsResponse = await testClient.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            member.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");

        await using var connection = CreateHubConnection(testFactory, member.AccessToken);
        var previewReceived = new TaskCompletionSource<SignalRMessagePreviewUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRMessagePreviewUpdatedEvent>("MessagePreviewUpdated", payload =>
        {
            previewReceived.TrySetResult(payload);
        });

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var sendMessageResponse = await testClient.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("Check this out https://example.com/article"),
            owner.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendMessagePayload.Should().NotBeNull();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var completedTask = await Task.WhenAny(previewReceived.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(previewReceived.Task, "MessagePreviewUpdated event should be received within 15 seconds");

        var eventPayload = await previewReceived.Task;
        eventPayload.MessageId.Should().Be(sendMessagePayload!.MessageId.ToString());
        eventPayload.ChannelId.Should().Be(textChannel.ChannelId.ToString());
        eventPayload.Previews.Should().NotBeNull();
        eventPayload.Previews.Should().HaveCount(1);
        eventPayload.Previews[0].Url.Should().Be("https://example.com/article");
        eventPayload.Previews[0].Title.Should().Be("Example Title");
        eventPayload.Previews[0].Description.Should().Be("Example Description");
        eventPayload.Previews[0].SiteName.Should().Be("Example Site");
    }

    [Fact]
    public async Task MessagePreviewUpdated_WhenMessageHasNoUrl_ShouldNotReceiveEvent()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("No Preview Guild"),
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

        // Connect member via SignalR
        await using var connection = CreateHubConnection(_factory, member.AccessToken);
        var previewReceived = new TaskCompletionSource<SignalRMessagePreviewUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<SignalRMessagePreviewUpdatedEvent>("MessagePreviewUpdated", payload =>
        {
            previewReceived.TrySetResult(payload);
        });

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Act: send a message without URLs
        var sendMessageResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("Hello world, no links here!"),
            owner.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert: the event should NOT arrive (no URLs to resolve)
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(previewReceived.Task, Task.Delay(TimeSpan.FromSeconds(4), timeout.Token));
        completedTask.Should().NotBe(previewReceived.Task,
            "MessagePreviewUpdated should not be received when message has no URLs");
    }

    private static HubConnection CreateHubConnection(WebApplicationFactory<Program> factory, string accessToken)
    {
        var baseAddress = factory.Server.BaseAddress;
        var hubUri = new Uri(baseAddress, "/hubs/realtime");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();
    }

    private sealed record SignalRMessagePreviewUpdatedEvent(
        string MessageId,
        string? ChannelId,
        string? ConversationId,
        string? GuildId,
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
