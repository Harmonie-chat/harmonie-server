using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Harmonie.Application.Features.Uploads.UploadFile;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class CreateGuildTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CreateGuildTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GuildPrimaryFlow_CreateInviteSendRead_ShouldSucceed()
    {
        var userA = await RegisterAsync();
        var userB = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Harmonie Guild"),
            userA.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();
        createGuildPayload!.IconFileId.Should().BeNull();
        createGuildPayload.Icon.Should().BeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(userB.UserId),
            userA.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteMemberResponse>();
        invitePayload.Should().NotBeNull();

        invitePayload!.UserId.Should().Be(userB.UserId);
        invitePayload.Role.Should().Be("Member");

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            userB.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        channelsPayload!.Channels.Should().HaveCount(2);
        channelsPayload.Channels.Should().Contain(channel => channel.Name == "general" && channel.Type == "Text");
        channelsPayload.Channels.Should().Contain(channel => channel.Name == "General Voice" && channel.Type == "Voice");

        var textChannel = channelsPayload.Channels.First(channel => channel.Type == "Text");

        var sendMessageResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("Hello team"),
            userA.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendMessagePayload.Should().NotBeNull();

        sendMessagePayload!.Content.Should().Be("Hello team");

        var getMessagesResponse = await SendAuthorizedGetAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            userB.AccessToken);
        getMessagesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getMessagesPayload = await getMessagesResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        getMessagesPayload.Should().NotBeNull();

        getMessagesPayload!.Items.Should().Contain(item => item.MessageId == sendMessagePayload.MessageId);
        getMessagesPayload.Items.Should().Contain(item => item.Content == "Hello team");
    }

    [Fact]
    public async Task ListUserGuilds_ShouldReturnOwnedAndInvitedGuilds()
    {
        var owner = await RegisterAsync();
        var inviter = await RegisterAsync();

        var ownerGuildOneResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Guild One"),
            owner.AccessToken);
        ownerGuildOneResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var ownerGuildOne = await ownerGuildOneResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        ownerGuildOne.Should().NotBeNull();

        var ownerGuildTwoResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Guild Two"),
            owner.AccessToken);
        ownerGuildTwoResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var ownerGuildTwo = await ownerGuildTwoResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        ownerGuildTwo.Should().NotBeNull();

        var inviterGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Inviter Guild"),
            inviter.AccessToken);
        inviterGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var inviterGuild = await inviterGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        inviterGuild.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{inviterGuild!.GuildId}/members/invite",
            new InviteMemberRequest(owner.UserId),
            inviter.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<ListUserGuildsResponse>();
        listPayload.Should().NotBeNull();

        listPayload!.Guilds.Should().HaveCount(3);
        listPayload.Guilds.Should().Contain(guild => guild.GuildId == ownerGuildOne!.GuildId && guild.Role == "Admin");
        listPayload.Guilds.Should().Contain(guild => guild.GuildId == ownerGuildTwo!.GuildId && guild.Role == "Admin");
        listPayload.Guilds.Should().Contain(guild => guild.GuildId == inviterGuild.GuildId && guild.Role == "Member");
        listPayload.Guilds.Should().OnlyContain(guild => guild.IconFileId == null && guild.Icon == null);
    }

    [Fact]
    public async Task CreateGuild_WithIconFields_ShouldPersistIconData()
    {
        var owner = await RegisterAsync();
        var iconFileId = await UploadFileAsync(owner.AccessToken, "guild-icon-create.png", "image/png", "guild create icon");

        var createResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new
            {
                name = "Guild With Icon",
                iconFileId,
                icon = new { color = "#7C3AED", name = "sword", bg = "#1F2937" }
            },
            owner.AccessToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createPayload.Should().NotBeNull();
        createPayload!.IconFileId.Should().Be(iconFileId);
        createPayload.Icon.Should().NotBeNull();
        createPayload.Icon!.Color.Should().Be("#7C3AED");
        createPayload.Icon.Name.Should().Be("sword");
        createPayload.Icon.Bg.Should().Be("#1F2937");

        var listResponse = await SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<ListUserGuildsResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Guilds.Should().Contain(guild =>
            guild.GuildId == createPayload.GuildId
            && guild.IconFileId == iconFileId
            && guild.Icon != null
            && guild.Icon.Color == "#7C3AED"
            && guild.Icon.Name == "sword"
            && guild.Icon.Bg == "#1F2937");
    }

    [Fact]
    public async Task CreateGuild_WithPartialIconFields_ShouldPersistProvidedFields()
    {
        var owner = await RegisterAsync();

        var createResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new
            {
                name = "Partial Icon Guild",
                icon = new { color = "#F59E0B" }
            },
            owner.AccessToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createPayload.Should().NotBeNull();
        createPayload!.IconFileId.Should().BeNull();
        createPayload.Icon.Should().NotBeNull();
        createPayload.Icon!.Color.Should().Be("#F59E0B");
        createPayload.Icon.Name.Should().BeNull();
        createPayload.Icon.Bg.Should().BeNull();
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
            Content = JsonContent.Create(payload, options: _jsonOptions)
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

    private async Task<string> UploadFileAsync(
        string accessToken,
        string fileName,
        string contentType,
        string content)
    {
        using var multipart = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/files/uploads")
        {
            Content = multipart
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<UploadFileResponse>();
        payload.Should().NotBeNull();
        payload!.FileId.Should().NotBeNullOrWhiteSpace();
        return payload.FileId;
    }
}
