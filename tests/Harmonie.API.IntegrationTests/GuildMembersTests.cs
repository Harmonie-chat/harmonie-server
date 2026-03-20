using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Channels.SendMessage;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildChannels;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Harmonie.Application.Features.Guilds.InviteMember;
using Harmonie.Application.Features.Guilds.TransferOwnership;
using Harmonie.Application.Features.Guilds.UpdateMemberRole;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class GuildMembersTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public GuildMembersTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGuildMembers_WhenRequesterIsMember_ShouldReturnGuildMembers()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Members Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var membersResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members",
            member.AccessToken);
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var membersPayload = await membersResponse.Content.ReadFromJsonAsync<GetGuildMembersResponse>();
        membersPayload.Should().NotBeNull();

        membersPayload!.GuildId.Should().Be(createGuildPayload.GuildId);
        membersPayload.Members.Should().HaveCount(2);
        membersPayload.Members.Should().Contain(x => x.UserId == owner.UserId && x.Role == "Admin");
        membersPayload.Members.Should().Contain(x => x.UserId == member.UserId && x.Role == "Member");
    }

    [Fact]
    public async Task InviteMember_WhenNonAdminInvites_ShouldReturnForbidden()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();
        var target = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(target.UserId),
            outsider.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();

        error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteForbidden);
    }

    [Fact]
    public async Task InviteMember_WhenTargetUserDoesNotExist_ShouldReturnNotFound()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Missing Invite Target Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(Guid.NewGuid().ToString()),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await inviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.InviteTargetNotFound);
    }

    [Fact]
    public async Task InviteMember_WhenTargetUserIsAlreadyMember_ShouldReturnConflict()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Duplicate Invite Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var firstInviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        firstInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondInviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        secondInviteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await secondInviteResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberAlreadyExists);
    }

    [Fact]
    public async Task LeaveGuild_WhenMemberLeaves_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Leave Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/leave",
            member.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task LeaveGuild_WhenOwnerLeaves_ShouldReturn409()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Leave Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/leave",
            owner.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await leaveResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotLeave);
    }

    [Fact]
    public async Task LeaveGuild_WhenNotMember_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Not Member Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/leave",
            outsider.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await leaveResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task LeaveGuild_WhenNotAuthenticated_ShouldReturn401()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Auth Leave Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var leaveResponse = await _client.PostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/leave",
            null);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LeaveGuild_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var leaveResponse = await SendAuthorizedPostNoBodyAsync(
            $"/api/guilds/{nonExistentGuildId}/leave",
            user.AccessToken);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await leaveResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task TransferOwnership_WhenOwnerTransfersToMember_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/owner/transfer",
            new TransferOwnershipRequest(member.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferOwnership_WhenNonOwnerTriesToTransfer_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var otherMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Owner Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(otherMember.UserId),
            owner.AccessToken);

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/owner/transfer",
            new TransferOwnershipRequest(otherMember.UserId),
            member.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task TransferOwnership_WhenOwnerTransfersToSelf_ShouldReturn409()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Self Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/owner/transfer",
            new TransferOwnershipRequest(owner.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerTransferToSelf);
    }

    [Fact]
    public async Task TransferOwnership_WhenNewOwnerIsNotMember_ShouldReturn404()
    {
        var owner = await RegisterAsync();
        var nonMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Transfer Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/owner/transfer",
            new TransferOwnershipRequest(nonMember.UserId),
            owner.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
    }

    [Fact]
    public async Task TransferOwnership_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var target = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var transferResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{nonExistentGuildId}/owner/transfer",
            new TransferOwnershipRequest(target.UserId),
            user.AccessToken);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await transferResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task TransferOwnership_WhenNotAuthenticated_ShouldReturn401()
    {
        var target = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();

        var transferResponse = await _client.PostAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/owner/transfer",
            new TransferOwnershipRequest(target.UserId));
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveMember_WhenAdminRemovesMember_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Remove Member Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveMember_WhenNonAdminTriesToRemove_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var otherMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Admin Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(otherMember.UserId),
            owner.AccessToken);

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{otherMember.UserId}",
            member.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task RemoveMember_WhenNotAuthenticated_ShouldReturn401()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Unauthenticated Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await _client.DeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{member.UserId}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveMember_WhenAdminTriesToRemoveOwner_ShouldReturn409()
    {
        // The owner is the only Admin in a newly created guild.
        // When the owner (admin) tries to remove themselves, the endpoint
        // must reject with 409 because the owner cannot be removed.
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{owner.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerCannotBeRemoved);
    }

    [Fact]
    public async Task RemoveMember_WhenAdminTriesToRemoveNonMember_ShouldReturn404()
    {
        var owner = await RegisterAsync();
        var nonMember = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Member Remove Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var removeResponse = await SendAuthorizedDeleteAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{nonMember.UserId}",
            owner.AccessToken);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await removeResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.MemberNotFound);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenAdminPromotesMember_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Promote Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var inviteResponse = await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRoleResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenAdminDemotesAdmin_ShouldReturn204()
    {
        var owner = await RegisterAsync();
        var otherAdmin = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Demote Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(otherAdmin.UserId),
            owner.AccessToken);

        await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{otherAdmin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            owner.AccessToken);

        var demoteResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{otherAdmin.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Member),
            owner.AccessToken);
        demoteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenNonAdminTriesToChangeRole_ShouldReturn403()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();
        var target = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Non Admin Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/invite",
            new InviteMemberRequest(target.UserId),
            owner.AccessToken);

        var updateRoleResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{target.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            member.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenAdminTriesToChangeOwnerRole_ShouldReturn409()
    {
        var owner = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Owner Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var updateRoleResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/{owner.UserId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Member),
            owner.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.OwnerRoleCannotBeChanged);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenGuildNotFound_ShouldReturn404()
    {
        var user = await RegisterAsync();
        var nonExistentGuildId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var updateRoleResponse = await SendAuthorizedPutAsync(
            $"/api/guilds/{nonExistentGuildId}/members/{targetId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin),
            user.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.NotFound);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenNotAuthenticated_ShouldReturn401()
    {
        var nonExistentGuildId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var updateRoleResponse = await _client.PutAsJsonAsync(
            $"/api/guilds/{nonExistentGuildId}/members/{targetId}/role",
            new UpdateMemberRoleRequest(GuildRoleInput.Admin));
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateMemberRole_WhenInvalidRole_ShouldReturn400()
    {
        var owner = await RegisterAsync();
        var member = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Invalid Role Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await SendAuthorizedPostAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/members/invite",
            new InviteMemberRequest(member.UserId),
            owner.AccessToken);

        var updateRoleResponse = await SendAuthorizedPutRawAsync(
            $"/api/guilds/{createGuildPayload.GuildId}/members/{member.UserId}/role",
            """{"role":"Owner"}""",
            owner.AccessToken);
        updateRoleResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await updateRoleResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task SendMessage_WhenChannelIsVoice_ShouldReturnConflict()
    {
        var user = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Voice Guild"),
            user.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            user.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var voiceChannel = channelsPayload!.Channels.First(channel => channel.Type == "Voice");

        var sendMessageResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{voiceChannel.ChannelId}/messages",
            new SendMessageRequest("Should fail"),
            user.AccessToken);
        sendMessageResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await sendMessageResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();

        error!.Code.Should().Be(ApplicationErrorCodes.Channel.NotText);
    }

    [Fact]
    public async Task SendMessage_WhenRateLimitExceeded_ShouldReturnTooManyRequests()
    {
        var user = await RegisterAsync();

        var createGuildResponse = await SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Rate Limit Guild"),
            user.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        var channelsResponse = await SendAuthorizedGetAsync(
            $"/api/guilds/{createGuildPayload!.GuildId}/channels",
            user.AccessToken);
        channelsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var channelsPayload = await channelsResponse.Content.ReadFromJsonAsync<GetGuildChannelsResponse>();
        channelsPayload.Should().NotBeNull();

        var textChannel = channelsPayload!.Channels.First(channel => channel.Type == "Text");

        for (var i = 0; i < 40; i++)
        {
            var sendResponse = await SendAuthorizedPostAsync(
                $"/api/channels/{textChannel.ChannelId}/messages",
                new SendMessageRequest($"msg-{i}"),
                user.AccessToken);

            sendResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var throttledResponse = await SendAuthorizedPostAsync(
            $"/api/channels/{textChannel.ChannelId}/messages",
            new SendMessageRequest("msg-over-limit"),
            user.AccessToken);

        throttledResponse.StatusCode.Should().Be((HttpStatusCode)429);
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

    private async Task<HttpResponseMessage> SendAuthorizedPostNoBodyAsync(
        string uri,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
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

    private async Task<HttpResponseMessage> SendAuthorizedPutAsync<TRequest>(
        string uri,
        TRequest payload,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(payload, options: _jsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthorizedPutRawAsync(
        string uri,
        string json,
        string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }
}
