using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
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
        await using var connection = CreateHubConnection(member.AccessToken);
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

    private HubConnection CreateHubConnection(string accessToken)
    {
        var baseAddress = _factory.Server.BaseAddress;
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
}
