using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.CreateGuildInvite;

namespace Harmonie.API.IntegrationTests.Common;

public static class GuildTestHelper
{
    public static async Task<CreateGuildResponse> CreateGuildAsync(
        HttpClient client,
        string name,
        string accessToken)
    {
        var response = await client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest(name),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await response.Content.ReadFromJsonAsync<CreateGuildResponse>();
        guild.Should().NotBeNull();
        return guild!;
    }

    public static async Task<Guid> CreateGuildAndGetIdAsync(
        HttpClient client,
        string accessToken,
        string guildName)
    {
        var guild = await CreateGuildAsync(client, guildName, accessToken);
        return guild.GuildId;
    }

    public static async Task InviteMemberAsync(
        HttpClient client,
        Guid guildId,
        string ownerAccessToken,
        string memberAccessToken)
    {
        var invite = await CreateInviteAsync(client, guildId, ownerAccessToken);

        var acceptResponse = await client.SendAuthorizedPostNoBodyAsync(
            $"/api/invites/{invite.Code}/accept",
            memberAccessToken);
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public static async Task<CreateGuildInviteResponse> CreateInviteAsync(
        HttpClient client,
        Guid guildId,
        string accessToken,
        int? maxUses = null)
    {
        var response = await client.SendAuthorizedPostAsync(
            $"/api/guilds/{guildId}/invites",
            new CreateGuildInviteRequest(MaxUses: maxUses),
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var invite = await response.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();
        invite.Should().NotBeNull();
        return invite!;
    }
}
