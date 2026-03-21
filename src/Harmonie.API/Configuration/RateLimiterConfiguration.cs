using System.Threading.RateLimiting;
using Harmonie.Application.Common;

namespace Harmonie.API.Configuration;

public static class RateLimiterConfiguration
{
    public static IServiceCollection AddApiRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("message-post", httpContext =>
            {
                var partitionKey = ResolvePartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 40,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    private static string ResolvePartitionKey(HttpContext httpContext)
    {
        if (httpContext.TryGetAuthenticatedUserId(out var userId) && userId is not null)
            return $"user:{userId}";

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{remoteIp}";
    }
}
