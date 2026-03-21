using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Harmonie.Application.Common;
using Harmonie.Application.Features.Auth.Register;
using Harmonie.Application.Features.Users.SearchUsers;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Harmonie.API.IntegrationTests;

public sealed class SearchUsersEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SearchUsersEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchUsers_WhenAuthenticated_ShouldReturnPrefixAndContainsMatches()
    {
        var caller = await RegisterAsync();
        var token = Guid.NewGuid().ToString("N")[..6];
        var alpha = await RegisterAsync(usernamePrefix: $"{token}aa");
        var beta = await RegisterAsync(usernamePrefix: "betauser");

        await UpdateDisplayNameAsync(alpha.AccessToken, $"{token} Alpha");
        await UpdateDisplayNameAsync(beta.AccessToken, $"The {token} Beta");

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/users/search?q={token}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SearchUsersResponse>();
        payload.Should().NotBeNull();
        payload!.Users.Should().Contain(user => user.Username == alpha.Username);
        payload.Users.Should().Contain(user => user.Username == beta.Username);
        payload.Users[0].Username.Should().Be(alpha.Username);
        payload.Users[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task SearchUsers_WhenScopedToGuild_ShouldReturnOnlyGuildMembers()
    {
        var owner = await RegisterAsync();
        var guildMember = await RegisterAsync(usernamePrefix: "alexmember");
        var outsider = await RegisterAsync(usernamePrefix: "alexoutsider");

        await UpdateDisplayNameAsync(guildMember.AccessToken, "Alex Member");
        await UpdateDisplayNameAsync(outsider.AccessToken, "Alex Outsider");

        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "User Search Guild");
        await GuildTestHelper.InviteMemberAsync(_client, guildId, guildMember.UserId, owner.AccessToken);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/users/search?q=alex&guildId={guildId}",
            owner.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SearchUsersResponse>();
        payload.Should().NotBeNull();
        payload!.Users.Should().ContainSingle(user => user.UserId == guildMember.UserId);
        payload.Users.Should().NotContain(user => user.UserId == outsider.UserId);
    }

    [Fact]
    public async Task SearchUsers_WhenCallerIsNotGuildMember_ShouldReturnForbidden()
    {
        var owner = await RegisterAsync();
        var outsider = await RegisterAsync();
        var guildId = await GuildTestHelper.CreateGuildAndGetIdAsync(_client, owner.AccessToken, "Forbidden User Search Guild");

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/users/search?q=al&guildId={guildId}",
            outsider.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Guild.AccessDenied);
    }

    [Fact]
    public async Task SearchUsers_ShouldExcludeInactiveUsers()
    {
        var caller = await RegisterAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        var activeUser = await RegisterAsync(usernamePrefix: $"{token}a");
        var inactiveUser = await RegisterAsync(usernamePrefix: $"{token}b");

        await UpdateDisplayNameAsync(activeUser.AccessToken, $"{token} Active");
        await UpdateDisplayNameAsync(inactiveUser.AccessToken, $"{token} Blocked");
        await DeactivateUserAsync(inactiveUser.UserId);

        var response = await _client.SendAuthorizedGetAsync(
            $"/api/users/search?q={token}",
            caller.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<SearchUsersResponse>();
        payload.Should().NotBeNull();
        payload!.Users.Should().Contain(user => user.UserId == activeUser.UserId);
        payload.Users.Should().NotContain(user => user.UserId == inactiveUser.UserId);
    }

    [Fact]
    public async Task SearchUsers_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/users/search?q=al");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var error = await response.Content.ReadFromJsonAsync<ApplicationError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(ApplicationErrorCodes.Auth.InvalidCredentials);
    }

    private async Task<RegisterResponse> RegisterAsync(string? usernamePrefix = null)
    {
        var usernameBase = usernamePrefix ?? $"user{Guid.NewGuid():N}";
        var request = new RegisterRequest(
            Email: $"test{Guid.NewGuid():N}@harmonie.chat",
            Username: $"{usernameBase}{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private async Task UpdateDisplayNameAsync(string accessToken, string displayName)
    {
        var response = await _client.SendAuthorizedPatchAsync(
            "/api/users/me",
            new { displayName },
            accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task DeactivateUserAsync(string userId)
    {
        if (!UserId.TryParse(userId, out var parsedUserId) || parsedUserId is null)
            throw new InvalidOperationException("Generated user ID could not be parsed.");

        await using var scope = _factory.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByIdAsync(parsedUserId);
        user.Should().NotBeNull();

        var deactivateResult = user!.Deactivate();
        deactivateResult.IsFailure.Should().BeFalse();

        await userRepository.UpdateAsync(user);
    }
}
