using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harmonie.API.SignalRDoc.Generator;
using Harmonie.API.SignalRDoc.Middleware;
using Harmonie.API.SignalRDoc.Models;
using Microsoft.Extensions.Options;

namespace Harmonie.API.SignalRDoc.Extensions;

public static class SignalRDocExtensions
{
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IServiceCollection AddSignalRAsyncApiDoc(
        this IServiceCollection services,
        Action<SignalRDocOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<HubDiscovery>();
        services.AddSingleton<SchemaGenerator>();
        services.AddSingleton<AsyncApiGenerator>();
        return services;
    }

    public static IEndpointRouteBuilder MapSignalRAsyncApiDoc(this IEndpointRouteBuilder app)
    {
        app.MapGet("/asyncapi/v1.json", (AsyncApiGenerator generator) =>
            Results.Json(generator.Generate(), SerializerOptions));

        app.MapGet("/asyncapi/ui", () =>
            Results.Content(EmbeddedAssets.LoadHtmlPage(), "text/html"));

        return app;
    }
}

public sealed class SignalRDocOptions
{
    public string Title { get; set; } = "SignalR API";
    public string Version { get; set; } = "1.0.0";
    public string? Description { get; set; }
    public string? ServerHost { get; set; }
    public Dictionary<Type, string> HubRoutes { get; set; } = new();
    public List<Assembly> Assemblies { get; set; } = new();
}
