using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.GetGuildMembers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class GuildInvitesTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GuildInvitesTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGuildMembers_WhenRequesterIsMember_ShouldReturnGuildMembers()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);
        var member = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Members Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createGuildPayload = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();
        createGuildPayload.Should().NotBeNull();

        await GuildTestHelper.InviteMemberAsync(_client, createGuildPayload!.GuildId, owner.AccessToken, member.AccessToken);

        var membersResponse = await _client.SendAuthorizedGetAsync(
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
}
