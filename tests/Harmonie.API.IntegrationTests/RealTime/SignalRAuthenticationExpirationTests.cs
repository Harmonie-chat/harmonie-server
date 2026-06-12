using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Harmonie.API.IntegrationTests.Common;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Harmonie.API.IntegrationTests;

/// <summary>
/// The realtime hub is mapped with CloseOnAuthenticationExpiration: the server
/// must close a connection once its access token expires, so stale sessions
/// (logout, ban, expired token) stop receiving events and clients reconnect
/// with a fresh token.
/// </summary>
public sealed class SignalRAuthenticationExpirationTests : IClassFixture<HarmonieWebApplicationFactory>
{
    private readonly HarmonieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SignalRAuthenticationExpirationTests(HarmonieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Connection_WhenAccessTokenExpires_ShouldBeClosedByServer()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);
        var shortLivedToken = CreateAccessToken(user.UserId, expiresInSeconds: 5);

        await using var connection = CreateHubConnection(shortLivedToken);

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        await StartAndWaitReadyAsync(connection);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var completedTask = await Task.WhenAny(closed.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        completedTask.Should().Be(closed.Task, "the server should close the connection once the access token expires");
    }

    [Fact]
    public async Task Connection_WithValidToken_ShouldStayOpenPastTheExpirationScanWindow()
    {
        var user = await AuthTestHelper.RegisterAsync(_client);

        await using var connection = CreateHubConnection(user.AccessToken);
        await StartAndWaitReadyAsync(connection);

        // Longer than the short-lived token window above: a valid token must survive it
        await Task.Delay(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken);

        connection.State.Should().Be(HubConnectionState.Connected);
    }

    private string CreateAccessToken(Guid userId, int expiresInSeconds)
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var secret = configuration["Jwt:Secret"];
        secret.Should().NotBeNullOrWhiteSpace();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: DateTime.UtcNow.AddSeconds(expiresInSeconds),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task StartAndWaitReadyAsync(HubConnection connection)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Ready", () => ready.TrySetResult());
        await connection.StartAsync();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private HubConnection CreateHubConnection(string accessToken)
    {
        var baseAddress = _client.BaseAddress ?? new Uri("http://localhost");
        var hubUri = new Uri(baseAddress, "/hubs/realtime");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }
}
