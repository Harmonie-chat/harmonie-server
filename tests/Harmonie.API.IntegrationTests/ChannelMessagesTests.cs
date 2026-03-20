using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.EditMessage;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.JoinVoiceChannel;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateChannel;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.ReorderChannels;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Uploads.UploadFile;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class ChannelMessagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ChannelMessagesTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendMessage_WithAttachmentFileIds_ShouldReturnCreatedAndExposeAttachmentsInList()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "attachment-channel");
        var uploadedFile = await UploadAttachmentAsync(author.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [uploadedFile.FileId]),
            author.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();
        sendPayload!.Attachments.Should().ContainSingle();
        sendPayload.Attachments[0].FileId.Should().Be(uploadedFile.FileId);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            author.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Attachments.Should().ContainSingle();
        listPayload.Items[0].Attachments[0].FileId.Should().Be(uploadedFile.FileId);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenAuthorDeletesOwnAttachment_ShouldReturn204AndRemoveAttachment()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "attachment-delete-channel");
        var uploadedFile = await UploadAttachmentAsync(author.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [uploadedFile.FileId]),
            author.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{sendPayload!.MessageId}/attachments/{uploadedFile.FileId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            author.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Attachments.Should().BeEmpty();

        var fileResponse = await SendAuthorizedGetAsync($"/api/files/{uploadedFile.FileId}", author.AccessToken);
        fileResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenMemberTriesToDeleteAnotherUsersAttachment_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Delete Attachment Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "attachment-auth-channel", guildId: guildId);
        var uploadedFile = await UploadAttachmentAsync(owner.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [uploadedFile.FileId]),
            owner.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{sendPayload!.MessageId}/attachments/{uploadedFile.FileId}",
            member.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenAttachmentIsNotOnMessage_ShouldReturn404()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "attachment-notfound-channel");
        var messageId = await SendMessageAndGetIdAsync(channelId, "message without attachment", author.AccessToken);
        var uploadedFile = await UploadAttachmentAsync(author.AccessToken, "unused.txt", "text/plain", "unused attachment");

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}/attachments/{uploadedFile.FileId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.AttachmentNotFound);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenNotAuthenticated_ShouldReturn401()
    {
        var deleteResponse = await _client.DeleteAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EditMessage_WhenAuthorEditsOwnMessage_ShouldReturn200WithUpdatedContent()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "edit-message-channel");
        var messageId = await SendMessageAndGetIdAsync(channelId, "original content", author.AccessToken);

        var editResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            new { content = "updated content" },
            author.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await editResponse.Content.ReadFromJsonAsync<EditMessageResponse>();
        payload.Should().NotBeNull();
        payload!.MessageId.Should().Be(messageId);
        payload.Content.Should().Be("updated content");
        payload.ChannelId.Should().Be(channelId);
        payload.AuthorUserId.Should().Be(author.UserId);
    }

    [Fact]
    public async Task EditMessage_WhenNonAuthorMemberTriesToEdit_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Edit Message Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "edit-auth-channel", guildId: guildId);
        var messageId = await SendMessageAndGetIdAsync(channelId, "owner's message", owner.AccessToken);

        var editResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            new { content = "member tampering" },
            member.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await editResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.EditForbidden);
    }

    [Fact]
    public async Task EditMessage_WhenNonMemberTriesToEdit_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "edit-nonmember-channel");
        var messageId = await SendMessageAndGetIdAsync(channelId, "owner's message", owner.AccessToken);

        var editResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            new { content = "outsider tampering" },
            outsider.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await editResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task EditMessage_WhenMessageNotFound_ShouldReturn404()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "edit-notfound-channel");
        var nonExistentMessageId = Guid.NewGuid();

        var editResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}/messages/{nonExistentMessageId}",
            new { content = "updated content" },
            author.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await editResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task EditMessage_WhenChannelNotFound_ShouldReturn404()
    {
        var author = await RegisterAsync();
        var nonExistentChannelId = Guid.NewGuid();
        var nonExistentMessageId = Guid.NewGuid();

        var editResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{nonExistentChannelId}/messages/{nonExistentMessageId}",
            new { content = "updated content" },
            author.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await editResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task EditMessage_WhenNotAuthenticated_ShouldReturn401()
    {
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var editResponse = await _client.PatchAsJsonAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            new { content = "anon edit" });
        editResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EditMessage_WhenContentIsEmpty_ShouldReturn400()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "edit-empty-channel");
        var messageId = await SendMessageAndGetIdAsync(channelId, "original content", author.AccessToken);

        var editResponse = await SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            new { content = "   " },
            author.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await editResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
    }

    // ─── DeleteMessage Tests ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteMessage_WhenAuthorDeletesOwnMessage_ShouldReturn204()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "delete-message-channel");
        var messageId = await SendMessageAndGetIdAsync(channelId, "message to delete", author.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMessage_WhenAdminDeletesAnotherUsersMessage_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Admin Delete Message Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "admin-delete-channel", guildId: guildId);
        var messageId = await SendMessageAndGetIdAsync(channelId, "member's message", member.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            owner.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMessage_WhenMemberTriesToDeleteAnotherUsersMessage_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Forbidden Delete Message Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "member-delete-msg-channel", guildId: guildId);
        var messageId = await SendMessageAndGetIdAsync(channelId, "owner's message", owner.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            member.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task DeleteMessage_WhenNonMemberTriesToDelete_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();

        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "outsider-delete-msg-channel");
        var messageId = await SendMessageAndGetIdAsync(channelId, "owner's message", owner.AccessToken);

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            outsider.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task DeleteMessage_WhenMessageNotFound_ShouldReturn404()
    {
        var author = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(author.AccessToken, "delete-notfound-channel");
        var nonExistentMessageId = Guid.NewGuid();

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{nonExistentMessageId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.NotFound);
    }

    [Fact]
    public async Task DeleteMessage_WhenChannelNotFound_ShouldReturn404()
    {
        var author = await RegisterAsync();
        var nonExistentChannelId = Guid.NewGuid();
        var nonExistentMessageId = Guid.NewGuid();

        var deleteResponse = await SendAuthorizedDeleteAsync(
            $"/api/channels/{nonExistentChannelId}/messages/{nonExistentMessageId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task DeleteMessage_WhenNotAuthenticated_ShouldReturn401()
    {
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var deleteResponse = await _client.DeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── JoinVoiceChannel Tests ───────────────────────────────────────────────

    [Fact]
    public async Task JoinVoiceChannel_WhenGuildMemberJoinsVoiceChannel_ShouldReturn200()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Voice Join Guild");
        var voiceChannelId = await GetDefaultVoiceChannelIdAsync(owner.AccessToken, guildId);

        var joinResponse = await SendAuthorizedPostWithoutBodyAsync(
            $"/api/channels/{voiceChannelId}/voice/join",
            owner.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await joinResponse.Content.ReadFromJsonAsync<JoinVoiceChannelResponse>();
        payload.Should().NotBeNull();
        payload!.Token.Should().NotBeNullOrWhiteSpace();
        payload.Url.Should().Be("ws://localhost:7880");
        payload.RoomName.Should().Be($"channel:{voiceChannelId}");
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenChannelIsText_ShouldReturn409()
    {
        var owner = await RegisterAsync();
        var channelId = await CreateChannelAndGetIdAsync(owner.AccessToken, "text-only-channel");

        var joinResponse = await SendAuthorizedPostWithoutBodyAsync(
            $"/api/channels/{channelId}/voice/join",
            owner.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await joinResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotVoice);
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenUserIsNotGuildMember_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Forbidden Voice Join Guild");
        var voiceChannelId = await GetDefaultVoiceChannelIdAsync(owner.AccessToken, guildId);

        var joinResponse = await SendAuthorizedPostWithoutBodyAsync(
            $"/api/channels/{voiceChannelId}/voice/join",
            outsider.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await joinResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.AccessDenied);
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenChannelDoesNotExist_ShouldReturn404()
    {
        var user = await RegisterAsync();

        var joinResponse = await SendAuthorizedPostWithoutBodyAsync(
            $"/api/channels/{Guid.NewGuid()}/voice/join",
            user.AccessToken);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await joinResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotFound);
    }

    [Fact]
    public async Task JoinVoiceChannel_WhenNotAuthenticated_ShouldReturn401()
    {
        var response = await _client.PostAsync(
            $"/api/channels/{Guid.NewGuid()}/voice/join",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── ReorderChannels ──────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderChannels_WhenAdminReordersChannels_ShouldReturn200WithNewPositions()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder Guild");

        var ch1 = await CreateChannelAndGetIdAsync(owner.AccessToken, "reorder-ch1", guildId, position: 10);
        var ch2 = await CreateChannelAndGetIdAsync(owner.AccessToken, "reorder-ch2", guildId, position: 11);

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch2, 0),
                new ReorderChannelsItemRequest(ch1, 1)
            ]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await reorderResponse.Content.ReadFromJsonAsync<ReorderChannelsResponse>();
        payload.Should().NotBeNull();
        payload!.GuildId.Should().Be(guildId);

        var reorderedCh1 = payload.Channels.First(c => c.ChannelId == ch1);
        var reorderedCh2 = payload.Channels.First(c => c.ChannelId == ch2);
        reorderedCh1.Position.Should().Be(1);
        reorderedCh2.Position.Should().Be(0);
    }

    [Fact]
    public async Task ReorderChannels_WhenVerifiedByGet_ShouldPersistNewOrder()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder Persist Guild");

        var ch1 = await CreateChannelAndGetIdAsync(owner.AccessToken, "persist-ch1", guildId, position: 10);
        var ch2 = await CreateChannelAndGetIdAsync(owner.AccessToken, "persist-ch2", guildId, position: 11);

        await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch2, 0),
                new ReorderChannelsItemRequest(ch1, 1)
            ]),
            owner.AccessToken);

        var getResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            owner.AccessToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channels = await getResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channels.Should().NotBeNull();

        var getCh1 = channels!.Channels.First(c => c.ChannelId == ch1);
        var getCh2 = channels.Channels.First(c => c.ChannelId == ch2);
        getCh1.Position.Should().Be(1);
        getCh2.Position.Should().Be(0);
    }

    [Fact]
    public async Task ReorderChannels_WhenNonAdminAttempts_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder NonAdmin Guild");
        await InviteMemberAsync(guildId, member.UserId, owner.AccessToken);

        var ch1 = await CreateChannelAndGetIdAsync(owner.AccessToken, "noadmin-ch1", guildId, position: 0);

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch1, 5)
            ]),
            member.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReorderChannels_WhenChannelNotInGuild_ShouldReturn404()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder NotFound Guild");

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(Guid.NewGuid().ToString(), 0)
            ]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReorderChannels_WhenEmptyList_ShouldReturn400()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder Empty Guild");

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReorderChannels_WhenNotAuthenticated_ShouldReturn401()
    {
        var response = await _client.PatchAsync(
            $"/api/guilds/{Guid.NewGuid()}/channels/reorder",
            JsonContent.Create(new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(Guid.NewGuid().ToString(), 0)
            ])));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReorderChannels_WhenDuplicateChannelId_ShouldReturn400()
    {
        var owner = await RegisterAsync();
        var guildId = await CreateGuildAndGetIdAsync(owner.AccessToken, "Reorder Dup Guild");
        var ch1 = await CreateChannelAndGetIdAsync(owner.AccessToken, "dup-ch1", guildId, position: 0);

        var reorderResponse = await SendAuthorizedPatchAsync(
            $"/api/guilds/{guildId}/channels/reorder",
            new ReorderChannelsRequest([
                new ReorderChannelsItemRequest(ch1, 0),
                new ReorderChannelsItemRequest(ch1, 1)
            ]),
            owner.AccessToken);
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

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

    private async Task<string> CreateGuildAndGetIdAsync(string accessToken, string guildName)
    {
        var response = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(guildName),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateGuildResponse>();
        payload.Should().NotBeNull();

        return payload!.GuildId;
    }

    private async Task InviteMemberAsync(string guildId, string userId, string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/members/invite",
            new InviteMemberRequest(userId),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> CreateChannelAndGetIdAsync(
        string accessToken,
        string name,
        string? guildId = null,
        int position = 0)
    {
        if (guildId is null)
        {
            guildId = await CreateGuildAndGetIdAsync(accessToken, $"Guild for {name}");
        }

        var response = await SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/channels",
            new CreateChannelRequest(name, ChannelTypeInput.Text, position),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<CreateChannelResponse>();
        payload.Should().NotBeNull();

        return payload!.ChannelId;
    }

    private async Task<string> GetDefaultVoiceChannelIdAsync(
        string accessToken,
        string guildId)
    {
        var response = await SendAuthorizedGetAsync(
            $"/api/guilds/{guildId}/channels",
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        payload.Should().NotBeNull();

        return payload!.Channels.First(channel => channel.Type == "Voice").ChannelId;
    }

    private async Task<string> SendMessageAndGetIdAsync(
        string channelId,
        string content,
        string accessToken)
    {
        var response = await SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(content),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();

        return payload!.MessageId;
    }

    private async Task<UploadFileResponse> UploadAttachmentAsync(
        string accessToken,
        string fileName,
        string contentType,
        string content)
    {
        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
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

    private async Task<HttpResponseMessage> SendAuthorizedPostWithoutBodyAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
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
            Content = JsonContent.Create(payload, options: _jsonOptions)
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

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
