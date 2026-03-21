using System.Text.Json;
using Harmonie.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Harmonie.API.Configuration;

public static class HealthCheckConfiguration
{
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres")
            .AddCheck<LiveKitHealthCheck>("livekit");

        return services;
    }

    public static void MapApiHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteResponseAsync
        })
        .WithName("HealthCheck")
        .WithTags("System");
    }

    private static Task WriteResponseAsync(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description
                })
        };

        return httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
