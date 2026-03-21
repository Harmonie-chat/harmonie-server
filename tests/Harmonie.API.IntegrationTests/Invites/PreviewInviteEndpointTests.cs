using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Guilds.CreateGuild;
using Harmonie.Application.Features.Guilds.CreateGuildInvite;
using Harmonie.Application.Features.Guilds.PreviewInvite;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class PreviewInviteEndpointTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PreviewInviteEndpointTests(HarmonieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PreviewInvite_WithValidCode_ShouldReturn200()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Preview Invite Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var createInviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(MaxUses: 10, ExpiresInHours: 24),
            owner.AccessToken);
        createInviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();

        var previewResponse = await _client.GetAsync($"/api/invites/{invite!.Code}");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await previewResponse.Content.ReadFromJsonAsync<PreviewInviteResponse>();
        preview.Should().NotBeNull();
        preview!.GuildName.Should().Be("Preview Invite Guild");
        preview.MemberCount.Should().BeGreaterThanOrEqualTo(1);
        preview.UsesCount.Should().Be(0);
        preview.MaxUses.Should().Be(10);
        preview.ExpiresAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task PreviewInvite_WithUnlimitedInvite_ShouldReturnNullLimits()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Unlimited Preview Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var createInviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        createInviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();

        var previewResponse = await _client.GetAsync($"/api/invites/{invite!.Code}");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await previewResponse.Content.ReadFromJsonAsync<PreviewInviteResponse>();
        preview.Should().NotBeNull();
        preview!.MaxUses.Should().BeNull();
        preview.ExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task PreviewInvite_WithoutAuthentication_ShouldStillWork()
    {
        var owner = await AuthTestHelper.RegisterAsync(_client);

        var createGuildResponse = await _client.SendAuthorizedPostAsync(
            "/api/guilds",
            new CreateGuildRequest("Anon Preview Guild"),
            owner.AccessToken);
        createGuildResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var guild = await createGuildResponse.Content.ReadFromJsonAsync<CreateGuildResponse>();

        var createInviteResponse = await _client.SendAuthorizedPostAsync(
            $"/api/guilds/{guild!.GuildId}/invites",
            new CreateGuildInviteRequest(),
            owner.AccessToken);
        createInviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateGuildInviteResponse>();

        // Use a raw HttpClient with no auth header
        var previewResponse = await _client.GetAsync($"/api/invites/{invite!.Code}");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PreviewInvite_WhenInviteNotFound_ShouldReturn404()
    {
        var previewResponse = await _client.GetAsync("/api/invites/ZZZZZZZZ");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await previewResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Invite.NotFound);
    }

    [Fact]
    public async Task PreviewInvite_WhenInvalidCodeFormat_ShouldReturn400()
    {
        var previewResponse = await _client.GetAsync("/api/invites/abc");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await previewResponse.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Common.ValidationFailed);
    }

    [Fact]
    public async Task PreviewInvite_WhenCodeContainsSpecialChars_ShouldReturn400()
    {
        var previewResponse = await _client.GetAsync("/api/invites/abc!@#$%");
        previewResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
