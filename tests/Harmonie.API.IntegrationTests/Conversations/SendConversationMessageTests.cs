using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.GetMessages;
using Harmonie.Application.Features.Conversations.SendMessage;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SendConversationMessageTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SendConversationMessageTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendConversationMessage_WhenCallerIsParticipant_ShouldCreateMessage()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("hello direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        payload.Should().NotBeNull();
        payload!.ConversationId.Should().Be(conversationId);
        payload.AuthorUserId.Should().Be(caller.UserId);
        payload.Content.Should().Be("hello direct");
    }

    [Fact]
    public async Task SendConversationMessage_WithAttachmentFileIds_ShouldCreateMessageAndExposeAttachmentsInList()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var target = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, target.UserId);
        var uploadedFileId = await UploadTestHelper.UploadFileAsync(_client, caller.AccessToken, "notes.txt", "text/plain", "attachment payload");

        var sendResponse = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("hello direct", [uploadedFileId]),
            caller.AccessToken);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var sendPayload = await sendResponse.Content.ReadFromJsonAsync<SendMessageResponse>();
        sendPayload.Should().NotBeNull();
        sendPayload!.Attachments.Should().ContainSingle();
        sendPayload.Attachments[0].FileId.Should().Be(uploadedFileId);

        var listResponse = await _client.SendAuthorizedGetAsync(
            $"/api/conversations/{conversationId}/messages",
            caller.AccessToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPayload = await listResponse.Content.ReadFromJsonAsync<GetMessagesResponse>();
        listPayload.Should().NotBeNull();
        listPayload!.Items.Should().ContainSingle();
        listPayload.Items[0].Attachments.Should().ContainSingle();
        listPayload.Items[0].Attachments[0].FileId.Should().Be(uploadedFileId);
    }

    [Fact]
    public async Task SendConversationMessage_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages",
            new SendMessageRequest("hello direct"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task SendConversationMessage_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var participantOne = await AuthTestHelper.RegisterAsync(_client);
        var participantTwo = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, participantOne.AccessToken, participantTwo.UserId);

        var response = await _client.SendAuthorizedPostAsync(
            $"/api/conversations/{conversationId}/messages",
            new SendMessageRequest("intrusion"),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task SendConversationMessage_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages",
            new SendMessageRequest("hello direct"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

}
