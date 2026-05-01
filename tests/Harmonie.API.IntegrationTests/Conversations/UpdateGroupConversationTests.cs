using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Conversations.UpdateGroupConversation;
using Xunit;

namespace Harmonie.API.IntegrationTests.Conversations;

public sealed class UpdateGroupConversationTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UpdateGroupConversationTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdateGroupConversation_WhenParticipantUpdatesName_ShouldReturnOk()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var callerReg = await AuthTestHelper.RegisterAsync(_client, $"caller_{tag}");
        var otherReg = await AuthTestHelper.RegisterAsync(_client, $"other_{tag}");

        var groupConversation = await ConversationTestHelper.CreateGroupConversationAsync(
            _client,
            callerReg.AccessToken,
            "Original Name",
            [callerReg.UserId, otherReg.UserId]);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/conversations/{groupConversation.ConversationId}",
            new UpdateGroupConversationRequest("New Name"),
            callerReg.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateGroupConversationResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.ConversationId.Should().Be(groupConversation.ConversationId);
        payload.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateGroupConversation_WhenConversationDoesNotExist_ShouldReturnNotFound()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/conversations/{Guid.NewGuid()}",
            new UpdateGroupConversationRequest("New Name"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.NotFound);
    }

    [Fact]
    public async Task UpdateGroupConversation_WhenCallerIsNotParticipant_ShouldReturnForbidden()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var participantOne = await AuthTestHelper.RegisterAsync(_client, $"p1_{tag}");
        var participantTwo = await AuthTestHelper.RegisterAsync(_client, $"p2_{tag}");
        var outsider = await AuthTestHelper.RegisterAsync(_client, $"outsider_{tag}");

        var groupConversation = await ConversationTestHelper.CreateGroupConversationAsync(
            _client,
            participantOne.AccessToken,
            "Test Group",
            [participantOne.UserId, participantTwo.UserId]);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/conversations/{groupConversation.ConversationId}",
            new UpdateGroupConversationRequest("New Name"),
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.AccessDenied);
    }

    [Fact]
    public async Task UpdateGroupConversation_WhenConversationIsDirect_ShouldReturnBadRequest()
    {
        var caller = await AuthTestHelper.RegisterAsync(_client);
        var other = await AuthTestHelper.RegisterAsync(_client);
        var conversationId = await ConversationTestHelper.OpenConversationAsync(_client, caller.AccessToken, other.UserId);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/conversations/{conversationId}",
            new UpdateGroupConversationRequest("New Name"),
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>(TestContext.Current.CancellationToken);
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Conversation.InvalidConversationType);
    }

    [Fact]
    public async Task UpdateGroupConversation_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/conversations/{Guid.NewGuid()}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new UpdateGroupConversationRequest("New Name"))
        };
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateGroupConversation_WhenNameIsEmpty_ShouldReturnValidationFailed()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var callerReg = await AuthTestHelper.RegisterAsync(_client, $"caller_{tag}");
        var otherReg = await AuthTestHelper.RegisterAsync(_client, $"other_{tag}");

        var groupConversation = await ConversationTestHelper.CreateGroupConversationAsync(
            _client,
            callerReg.AccessToken,
            "Original Name",
            [callerReg.UserId, otherReg.UserId]);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/conversations/{groupConversation.ConversationId}",
            new UpdateGroupConversationRequest(""),
            callerReg.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateGroupConversation_WhenNameExceedsMaxLength_ShouldReturnValidationFailed()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var callerReg = await AuthTestHelper.RegisterAsync(_client, $"caller_{tag}");
        var otherReg = await AuthTestHelper.RegisterAsync(_client, $"other_{tag}");

        var groupConversation = await ConversationTestHelper.CreateGroupConversationAsync(
            _client,
            callerReg.AccessToken,
            "Original Name",
            [callerReg.UserId, otherReg.UserId]);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/conversations/{groupConversation.ConversationId}",
            new UpdateGroupConversationRequest(new string('a', 101)),
            callerReg.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateGroupConversation_SecondParticipantCanAlsoUpdate()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var callerReg = await AuthTestHelper.RegisterAsync(_client, $"caller_{tag}");
        var otherReg = await AuthTestHelper.RegisterAsync(_client, $"other_{tag}");

        var groupConversation = await ConversationTestHelper.CreateGroupConversationAsync(
            _client,
            callerReg.AccessToken,
            "Original Name",
            [callerReg.UserId, otherReg.UserId]);

        var response = await _client.SendAuthorizedPatchAsync(
            $"/api/conversations/{groupConversation.ConversationId}",
            new UpdateGroupConversationRequest("Updated By Other"),
            otherReg.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<UpdateGroupConversationResponse>(TestContext.Current.CancellationToken);
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("Updated By Other");
    }
}
