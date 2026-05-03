using FluentAssertions;
using Harmonie.API.SignalRDoc.Extensions;
using Harmonie.API.SignalRDoc.Generator;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harmonie.API.Tests.SignalRDoc;

public sealed class AsyncApiGeneratorTests
{
    private static AsyncApiGenerator CreateGenerator(Action<SignalRDocOptions>? configure = null)
    {
        var options = new SignalRDocOptions
        {
            Title = "Test API",
            Version = "0.1.0",
            Assemblies = { typeof(FakeHub).Assembly },
        };
        configure?.Invoke(options);

        return new AsyncApiGenerator(
            Options.Create(options),
            new HubDiscovery(),
            new SchemaGenerator());
    }

    [Fact]
    public void Generate_ReturnsValidAsyncApiVersion()
    {
        var doc = CreateGenerator().Generate();
        doc.Asyncapi.Should().Be("3.1.0");
    }

    [Fact]
    public void Generate_PopulatesInfoFromOptions()
    {
        var doc = CreateGenerator(o =>
        {
            o.Title = "My Hub API";
            o.Version = "2.0.0";
            o.Description = "Test description";
        }).Generate();

        doc.Info.Title.Should().Be("My Hub API");
        doc.Info.Version.Should().Be("2.0.0");
        doc.Info.Description.Should().Be("Test description");
    }

    [Fact]
    public void Generate_CreatesChannelForFakeHub()
    {
        var doc = CreateGenerator().Generate();

        doc.Channels.Should().NotBeNull();
        doc.Channels.Should().ContainKey("Fake");
        doc.Channels!["Fake"].Address.Should().Be("/hubs/fake");
    }

    [Fact]
    public void Generate_CreatesOperationForClientToServerMethod()
    {
        var doc = CreateGenerator().Generate();

        doc.Operations.Should().NotBeNull();
        doc.Operations.Should().ContainKey("Fake.SendMessage");
        doc.Operations!["Fake.SendMessage"].Action.Should().Be("receive");
        doc.Operations["Fake.SendMessage"].Channel.Ref.Should().Be("#/channels/Fake");
    }

    [Fact]
    public void Generate_CreatesOperationForServerToClientMethod()
    {
        var doc = CreateGenerator().Generate();

        doc.Operations.Should().ContainKey("Fake.OnMessage");
        doc.Operations!["Fake.OnMessage"].Action.Should().Be("send");
    }

    [Fact]
    public void Generate_OperationWithNoPayload_HasNoPayloadInMessage()
    {
        var doc = CreateGenerator().Generate();

        var messageKey = "Fake.OnReady.Message";
        doc.Components!.Messages.Should().ContainKey(messageKey);
        doc.Components.Messages![messageKey].Payload.Should().BeNull();
    }

    [Fact]
    public void Generate_OperationWithSingleParam_HasPayload()
    {
        var doc = CreateGenerator().Generate();

        var messageKey = "Fake.OnMessage.Message";
        doc.Components!.Messages.Should().ContainKey(messageKey);
        doc.Components.Messages![messageKey].Payload.Should().NotBeNull();
    }

    [Fact]
    public void Generate_MessageReferencesAreConsistent()
    {
        var doc = CreateGenerator().Generate();

        var messageKey = "Fake.SendMessage.Message";
        var opRef = doc.Operations!["Fake.SendMessage"].Messages!.Single().Ref;
        opRef.Should().Be($"#/components/messages/{messageKey}");

        var channelRef = doc.Channels!["Fake"].Messages![messageKey].Ref;
        channelRef.Should().Be($"#/components/messages/{messageKey}");

        doc.Components!.Messages.Should().ContainKey(messageKey);
    }

    [Fact]
    public void Generate_WithServerHost_IncludesServersSection()
    {
        var doc = CreateGenerator(o => o.ServerHost = "wss://api.example.com").Generate();

        doc.Servers.Should().NotBeNull();
        doc.Servers!.Should().ContainKey("default");
        doc.Servers!["default"].Host.Should().Be("wss://api.example.com");
        doc.Servers["default"].Protocol.Should().Be("wss");
    }

    [Fact]
    public void Generate_WithoutServerHost_OmitsServersSection()
    {
        var doc = CreateGenerator().Generate();
        doc.Servers.Should().BeNull();
    }

    [Fact]
    public void Generate_ReturnsCachedDocumentOnSubsequentCalls()
    {
        var generator = CreateGenerator();
        var first = generator.Generate();
        var second = generator.Generate();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Generate_ComplexPayloadType_RegistersSchema()
    {
        var doc = CreateGenerator().Generate();

        // FakeHub has JoinRoom(string roomId, int userId) — multi-param creates a Parameters schema
        doc.Components!.Schemas!.Should().ContainKey("Fake.JoinRoom.Parameters");
    }
}
