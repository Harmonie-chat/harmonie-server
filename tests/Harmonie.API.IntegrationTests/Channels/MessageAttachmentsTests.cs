using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.GetMessages;
using Harmonie.Application.Features.Channels.SendMessage;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class MessageAttachmentsTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MessageAttachmentsTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendMessage_WithAttachmentAndNoContent_ShouldReturnCreatedAndExposeNullContent()
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, "attachment-only-channel");
        var fileId = await UploadTestHelper.UploadFileAsync(_client, author.AccessToken, "image.png", "image/png", "attachment payload");

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(null, [fileId]),
            author.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendPayload.Should().NotBeNull();
        sendPayload!.Content.Should().BeNull();
        sendPayload.Attachments.Should().ContainSingle();
        sendPayload.Attachments[0].FileId.Should().Be(fileId);

        var listResponse = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            author.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>(TestContext.Current.CancellationToken);
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Content.Should().BeNull();
        listPayload.Items[0].Attachments.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task SendMessage_WithNoContentAndNoAttachment_ShouldReturnBadRequest(string? content)
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, $"no-content-test-{Guid.NewGuid()}");

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest(content),
            author.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_WithWhitespaceContentAndAttachment_ShouldReturnCreated()
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, $"whitespace-attachment-{Guid.NewGuid():N}");
        var fileId = await UploadTestHelper.UploadFileAsync(_client, author.AccessToken, "image.png", "image/png", "attachment payload");

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("   ", [fileId]),
            author.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendPayload.Should().NotBeNull();
        sendPayload!.Attachments.Should().ContainSingle();
    }

    [Fact]
    public async Task SendMessage_WithAttachmentFileIds_ShouldReturnCreatedAndExposeAttachmentsInList()
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, "attachment-channel");
        var fileId = await UploadTestHelper.UploadFileAsync(_client, author.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [fileId]),
            author.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendPayload.Should().NotBeNull();
        sendPayload!.Attachments.Should().ContainSingle();
        sendPayload.Attachments[0].FileId.Should().Be(fileId);

        var listResponse = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            author.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>(TestContext.Current.CancellationToken);
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Attachments.Should().ContainSingle();
        listPayload.Items[0].Attachments[0].FileId.Should().Be(fileId);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenAuthorDeletesOwnAttachment_ShouldReturn204AndRemoveAttachment()
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, "attachment-delete-channel");
        var fileId = await UploadTestHelper.UploadFileAsync(_client, author.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [fileId]),
            author.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendPayload.Should().NotBeNull();

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{sendPayload!.MessageId}/attachments/{fileId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAuthorizedGetAsync(
            $"/api/channels/{channelId}/messages",
            author.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>(TestContext.Current.CancellationToken);
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Attachments.Should().BeEmpty();

        var fileResponse = await _client.SendAuthorizedGetAsync($"/api/files/{fileId}", author.AccessToken);
        fileResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenMemberTriesToDeleteAnotherUsersAttachment_ShouldReturn403()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Delete Attachment Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, owner.AccessToken, member.AccessToken);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "attachment-auth-channel", guildId: guildId);
        var fileId = await UploadTestHelper.UploadFileAsync(_client, owner.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/channels/{channelId}/messages",
            new SendMessageRequest("message with attachment", [fileId]),
            owner.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>(TestContext.Current.CancellationToken);
        sendPayload.Should().NotBeNull();

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{sendPayload!.MessageId}/attachments/{fileId}",
            member.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.DeleteForbidden);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenAttachmentIsNotOnMessage_ShouldReturn404()
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, "attachment-notfound-channel");
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "message without attachment", author.AccessToken);
        var fileId = await UploadTestHelper.UploadFileAsync(_client, author.AccessToken, "unused.txt", "text/plain", "unused attachment");

        var deleteResponse = await _client.SendAuthorizedDeleteAsync(
            $"/api/channels/{channelId}/messages/{messageId}/attachments/{fileId}",
            author.AccessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.AttachmentNotFound);
    }

    [Fact]
    public async Task DeleteMessageAttachment_WhenNotAuthenticated_ShouldReturn401()
    {
        var deleteResponse = await _client.DeleteAsync(
            $"/api/channels/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/attachments/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
