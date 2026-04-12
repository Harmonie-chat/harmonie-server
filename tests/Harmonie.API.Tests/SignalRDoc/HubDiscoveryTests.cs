using FluentAssertions;
using Harmonie.API.SignalRDoc.Generator;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace Harmonie.API.Tests.SignalRDoc;

// Fake types used as test fixtures
public interface IFakeClient
{
    Task OnMessage(string message, CancellationToken ct = default);
    Task OnReady(CancellationToken ct = default);
    Task OnMulti(string a, int b, CancellationToken ct = default);
}

public sealed class FakeHub : Hub<IFakeClient>
{
    public async Task SendMessage(string content) { await Task.CompletedTask; }
    public async Task JoinRoom(string roomId, int userId) { await Task.CompletedTask; }
    public override Task OnConnectedAsync() => base.OnConnectedAsync();
    public override Task OnDisconnectedAsync(Exception? exception) => base.OnDisconnectedAsync(exception);
}

public abstract class AbstractHub : Hub<IFakeClient> { }

public sealed class HubDiscoveryTests
{
    private readonly HubDiscovery _discovery = new();

    [Fact]
    public void Discover_FindsConcreteTypedHub()
    {
        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly });

        result.Should().Contain(h => h.HubType == typeof(FakeHub));
    }

    [Fact]
    public void Discover_IgnoresAbstractHubs()
    {
        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly });

        result.Should().NotContain(h => h.HubType == typeof(AbstractHub));
    }

    [Fact]
    public void Discover_ExtractsClientInterface()
    {
        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly });
        var hub = result.Single(h => h.HubType == typeof(FakeHub));

        hub.ClientInterface.Should().Be(typeof(IFakeClient));
    }

    [Fact]
    public void Discover_ExcludesLifecycleMethods()
    {
        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly });
        var hub = result.Single(h => h.HubType == typeof(FakeHub));

        hub.ClientToServerMethods.Should().NotContain(m => m.Name == "OnConnectedAsync");
        hub.ClientToServerMethods.Should().NotContain(m => m.Name == "OnDisconnectedAsync");
        hub.ClientToServerMethods.Should().NotContain(m => m.Name == "Dispose");
    }

    [Fact]
    public void Discover_ExtractsClientToServerMethods()
    {
        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly });
        var hub = result.Single(h => h.HubType == typeof(FakeHub));

        hub.ClientToServerMethods.Should().Contain(m => m.Name == "SendMessage");
        hub.ClientToServerMethods.Should().Contain(m => m.Name == "JoinRoom");
    }

    [Fact]
    public void Discover_ExtractsServerToClientMethods()
    {
        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly });
        var hub = result.Single(h => h.HubType == typeof(FakeHub));

        hub.ServerToClientMethods.Should().Contain(m => m.Name == "OnMessage");
        hub.ServerToClientMethods.Should().Contain(m => m.Name == "OnReady");
    }

    [Fact]
    public void Discover_ExcludesCancellationTokenParameters()
    {
        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly });
        var hub = result.Single(h => h.HubType == typeof(FakeHub));

        var onMessage = hub.ServerToClientMethods.Single(m => m.Name == "OnMessage");
        onMessage.Parameters.Should().NotContain(p => p.Type == typeof(CancellationToken));
        onMessage.Parameters.Should().ContainSingle(p => p.Type == typeof(string));

        var onReady = hub.ServerToClientMethods.Single(m => m.Name == "OnReady");
        onReady.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Discover_ExtractsMultipleParameters()
    {
        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly });
        var hub = result.Single(h => h.HubType == typeof(FakeHub));

        var joinRoom = hub.ClientToServerMethods.Single(m => m.Name == "JoinRoom");
        joinRoom.Parameters.Should().HaveCount(2);
        joinRoom.Parameters[0].Type.Should().Be(typeof(string));
        joinRoom.Parameters[1].Type.Should().Be(typeof(int));
    }

    [Fact]
    public void Discover_DerivesRouteFromHubName()
    {
        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly });
        var hub = result.Single(h => h.HubType == typeof(FakeHub));

        hub.Route.Should().Be("/hubs/fake");
    }

    [Fact]
    public void Discover_UsesRouteOverrideWhenProvided()
    {
        var overrides = new Dictionary<Type, string>
        {
            [typeof(FakeHub)] = "/custom/route",
        };

        var result = _discovery.Discover(new[] { typeof(FakeHub).Assembly }, overrides);
        var hub = result.Single(h => h.HubType == typeof(FakeHub));

        hub.Route.Should().Be("/custom/route");
    }
}
