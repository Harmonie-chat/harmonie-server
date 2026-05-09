using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.EditMessage;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Domain.Entities.Messages;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using ChannelSend = Harmonie.Application.Features.Channels.SendMessage;
using ConversationSend = Harmonie.Application.Features.Conversations.SendMessage;

namespace Harmonie.API.IntegrationTests;

public sealed class MessageMentionsIntegrationTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MessageMentionsIntegrationTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Channel mentions

    [Fact]
    public async Task SendChannelMessage_WithValidMention_ShouldReturn200WithMentionedUserIds()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"Mention{Guid.NewGuid():N}"[..16]);
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, member.AccessToken);

        var (channelId, _) = await GetTextChannelAsync(_client, guildId, owner.AccessToken);

        var request = new { content = "Hello @member", mentionedUserIds = new[] { member.UserId } };
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages", request, owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<ChannelSend.SendMessageResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.MentionedUserIds.Should().ContainSingle().Which.Should().Be(member.UserId);
    }

    [Fact]
    public async Task SendChannelMessage_SelfMention_ShouldSucceed()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"Self{Guid.NewGuid():N}"[..16]);
        var (channelId, _) = await GetTextChannelAsync(_client, guildId, owner.AccessToken);

        var request = new { content = "noting myself", mentionedUserIds = new[] { owner.UserId } };
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages", request, owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<ChannelSend.SendMessageResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.MentionedUserIds.Should().ContainSingle().Which.Should().Be(owner.UserId);
    }

    [Fact]
    public async Task SendChannelMessage_WithNonExistentMentionedUser_ShouldReturn404()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"Ghost{Guid.NewGuid():N}"[..16]);
        var (channelId, _) = await GetTextChannelAsync(_client, guildId, owner.AccessToken);

        var request = new { content = "Hello @ghost", mentionedUserIds = new[] { Guid.NewGuid() } };
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages", request, owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.MentionedUserNotFound);
    }

    [Fact]
    public async Task SendChannelMessage_WithNonMemberMention_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"Excl{Guid.NewGuid():N}"[..16]);
        var (channelId, _) = await GetTextChannelAsync(_client, guildId, owner.AccessToken);

        var request = new { content = "Hello @outsider", mentionedUserIds = new[] { outsider.UserId } };
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages", request, owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.MentionedUserNotMember);
    }

    [Fact]
    public async Task SendChannelMessage_WithMoreThan50Mentions_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"Mass{Guid.NewGuid():N}"[..16]);
        var (channelId, _) = await GetTextChannelAsync(_client, guildId, owner.AccessToken);

        var ids = Enumerable.Range(0, Message.MaxMentionedUsers + 1).Select(_ => Guid.NewGuid()).ToArray();
        var request = new { content = "mass mention", mentionedUserIds = ids };
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages", request, owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendChannelMessage_WithDuplicateMentions_ShouldReturn400()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"Dup{Guid.NewGuid():N}"[..16]);
        var (channelId, _) = await GetTextChannelAsync(_client, guildId, owner.AccessToken);

        var id = Guid.NewGuid();
        var request = new { content = "double mention", mentionedUserIds = new[] { id, id } };
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages", request, owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EditChannelMessage_ReplaceMentions_ShouldReturn200WithNewMentions()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"EditM{Guid.NewGuid():N}"[..16]);
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, member.AccessToken);

        var (channelId, _) = await GetTextChannelAsync(_client, guildId, owner.AccessToken);
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "original", owner.AccessToken);

        var editRequest = new { content = "edited with mention", mentionedUserIds = new[] { member.UserId } };
        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}/messages/{messageId}", editRequest, owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<EditMessageResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.MentionedUserIds.Should().ContainSingle().Which.Should().Be(member.UserId);
    }

    [Fact]
    public async Task EditChannelMessage_ClearMentions_ShouldReturn200WithEmptyMentions()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"Clear{Guid.NewGuid():N}"[..16]);
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, member.AccessToken);

        var (channelId, _) = await GetTextChannelAsync(_client, guildId, owner.AccessToken);

        // First send with a mention
        var sendRequest = new { content = "with mention", mentionedUserIds = new[] { member.UserId } };
        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages", sendRequest, owner.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<ChannelSend.SendMessageResponse>(TestContext.Current.CancellationToken);
        sendPayload.Should().NotBeNull();
        sendPayload!.MentionedUserIds.Should().NotBeEmpty();

        // Then clear mentions via edit
        var editRequest = new { content = "mentions cleared", mentionedUserIds = Array.Empty<Guid>() };
        var editResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}/messages/{sendPayload.MessageId}", editRequest, owner.AccessToken);

        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var editPayload = await editResponse.Content.ReadFromJsonAsync<EditMessageResponse>(TestContext.Current.CancellationToken);
        editPayload.Should().NotBeNull();
        editPayload!.MentionedUserIds.Should().BeEmpty();
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Conversation mentions

    [Fact]
    public async Task SendConversationMessage_WithValidMention_ShouldReturn201WithMentionedUserIds()
    {
        var userA = await AuthTestHelper.RegisterAsync(_client);
        var userB = await AuthTestHelper.RegisterAsync(_client);

        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, userA.AccessToken, userB.UserId);

        var request = new { content = "Hello @userB", mentionedUserIds = new[] { userB.UserId } };
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages", request, userA.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<ConversationSend.SendMessageResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.MentionedUserIds.Should().ContainSingle().Which.Should().Be(userB.UserId);
    }

    [Fact]
    public async Task SendConversationMessage_WithNonParticipantMention_ShouldReturn403()
    {
        var userA = await AuthTestHelper.RegisterAsync(_client);
        var userB = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);

        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, userA.AccessToken, userB.UserId);

        var request = new { content = "Hello @outsider", mentionedUserIds = new[] { outsider.UserId } };
        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages", request, userA.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.MentionedUserNotMember);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  GET returns mentions

    [Fact]
    public async Task GetChannelMessages_WithMentions_ShouldHydrateMentionedUserIds()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, $"GetM{Guid.NewGuid():N}"[..16]);
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, member.AccessToken);

        var (channelId, _) = await GetTextChannelAsync(_client, guildId, owner.AccessToken);

        var sendRequest = new { content = "Hello @member", mentionedUserIds = new[] { member.UserId } };
        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages", sendRequest, owner.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages", owner.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getPayload = await getResponse.Content.ReadFromJsonAsync<GetMessagesResponse>(TestContext.Current.CancellationToken);
        getPayload.Should().NotBeNull();
        getPayload!.Items.Should().ContainSingle(i => i.MentionedUserIds.Contains(member.UserId));
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Helpers

    private static async Task<(Guid ChannelId, string ChannelName)> GetTextChannelAsync(
        HttpClient client, Guid guildId, string accessToken)
    {
        var response = await client.SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels", accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetGuildChannelsResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();

        var textChannel = payload!.Channels.First(c => c.Type == "Text");
        return (textChannel.ChannelId, textChannel.Name);
    }
}
