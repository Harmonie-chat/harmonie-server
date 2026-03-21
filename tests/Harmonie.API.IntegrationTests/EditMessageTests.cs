using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Channels.EditMessage;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class EditMessageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EditMessageTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task EditMessage_WhenAuthorEditsOwnMessage_ShouldReturn200WithUpdatedContent()
    {
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, "edit-message-channel");
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "original content", author.AccessToken);

        var editResponse = await _client.SendAuthorizedPatchAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Edit Message Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, member.UserId, owner.AccessToken);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "edit-auth-channel", guildId: guildId);
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "owner's message", owner.AccessToken);

        var editResponse = await _client.SendAuthorizedPatchAsync(
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
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var outsider = await AuthTestHelper.RegisterAsync(_client);

        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, owner.AccessToken, "edit-nonmember-channel");
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "owner's message", owner.AccessToken);

        var editResponse = await _client.SendAuthorizedPatchAsync(
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
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, "edit-notfound-channel");
        var nonExistentMessageId = Guid.NewGuid();

        var editResponse = await _client.SendAuthorizedPatchAsync(
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
        var author = await AuthTestHelper.RegisterAsync(_client);
        var nonExistentChannelId = Guid.NewGuid();
        var nonExistentMessageId = Guid.NewGuid();

        var editResponse = await _client.SendAuthorizedPatchAsync(
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
        var author = await AuthTestHelper.RegisterAsync(_client);
        var channelId = await ChannelTestHelper.CreateChannelAndGetIdAsync(_client, author.AccessToken, "edit-empty-channel");
        var messageId = await ChannelTestHelper.SendMessageAndGetIdAsync(_client, channelId, "original content", author.AccessToken);

        var editResponse = await _client.SendAuthorizedPatchAsync(
            $"/api/channels/{channelId}/messages/{messageId}",
            new { content = "   " },
            author.AccessToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await editResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Message.ContentEmpty);
    }
}
