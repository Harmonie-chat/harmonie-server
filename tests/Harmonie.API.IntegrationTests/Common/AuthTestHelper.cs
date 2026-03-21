using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Harmonie.Application.Features.Auth.Register;

namespace Harmonie.API.IntegrationTests.Common;

public static class AuthTestHelper
{
    public static async Task<RegisterResponse> RegisterAsync(HttpClient client, string? token = null)
    {
        var usernameBase = token is not null ? $"u{token}" : $"user{Guid.NewGuid():N}";
        var emailPrefix = token is not null ? $"test{token}" : $"test{Guid.NewGuid():N}";

        var request = new RegisterRequest(
            Email: $"{emailPrefix}{Guid.NewGuid():N}@harmonie.chat",
            Username: $"{usernameBase}{Guid.NewGuid():N}"[..20],
            Password: "Test123!@#");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        payload.Should().NotBeNull();

        return payload!;
    }
}
