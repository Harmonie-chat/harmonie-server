using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.ListUserGuilds;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class CreateGuildTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CreateGuildTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GuildPrimaryFlow_CreateInviteSendRead_ShouldSucceed()
    {
        var userA = await AuthTestHelper.RegisterAsync(_client);
        var userB = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Harmonie Guild"),
            userA.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();
        createGuildPayload!.IconFileId.Should().BeNull();
        createGuildPayload.Icon.Should().BeNull();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(userB.UserId),
            userA.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var invitePayload = await inviteResponse.Content.ReadFromJsonAsync<InviteMemberResponse>();
        invitePayload.Should().NotBeNull();

        invitePayload!.UserId.Should().Be(userB.UserId);
        invitePayload.Role.Should().Be("Member");

        var channelsResponse = await _client.SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/channels",
            userB.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        channelsPayload!.Channels.Should().HaveCount(2);
        channelsPayload.Channels.Should().Contain(channel => channel.Name == "general" && channel.Type == "Text");
        channelsPayload.Channels.Should().Contain(channel => channel.Name == "General Voice" && channel.Type == "Voice");

        var textChannel = channelsPayload.Channels.First(channel => channel.Type == "Text");

        var sendMessageResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("Hello team"),
            userA.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendMessagePayload = await sendMessageResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendMessagePayload.Should().NotBeNull();

        sendMessagePayload!.Content.Should().Be("Hello team");

        var getMessagesResponse = await _client.SendAuthorizedGetAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var inviter = await AuthTestHelper.RegisterAsync(_client);

        var ownerGuildOneResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Guild One"),
            owner.AccessToken);
        ownerGuildOneResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var ownerGuildOne = await ownerGuildOneResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        ownerGuildOne.Should().NotBeNull();

        var ownerGuildTwoResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Guild Two"),
            owner.AccessToken);
        ownerGuildTwoResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var ownerGuildTwo = await ownerGuildTwoResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        ownerGuildTwo.Should().NotBeNull();

        var inviterGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Inviter Guild"),
            inviter.AccessToken);
        inviterGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var inviterGuild = await inviterGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        inviterGuild.Should().NotBeNull();

        var inviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{inviterGuild!.GuildId}/members/invite",
            new InviteMemberRequest(owner.UserId),
            inviter.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await _client.SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
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
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var iconFileId = await UploadTestHelper.UploadFileAsync(_client, owner.AccessToken, "guild-icon-create.png", "image/png", "guild create icon");

        var createResponse = await _client.SendAuthorizedPostAsync(
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

        var listResponse = await _client.SendAuthorizedGetAsync("/api/guilds", owner.AccessToken);
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
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createResponse = await _client.SendAuthorizedPostAsync(
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
}
