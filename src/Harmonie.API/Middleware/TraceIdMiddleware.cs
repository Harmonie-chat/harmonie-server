using System.Diagnostics;
using Serilog.Context;

namespace Harmonie.API.Middleware;

public sealed class TraceIdMiddleware
{
    private const string TraceIdItemKey = "TraceId";
    private const string ResponseHeaderName = "X-Trace-Id";
    private readonly RequestDelegate _next;

    public TraceIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToHexString()
                      ?? ActivityTraceId.CreateRandom().ToHexString();

        using (LogContext.PushProperty("TraceId", traceId))
        {
            context.Items[TraceIdItemKey] = traceId;
            context.Response.Headers[ResponseHeaderName] = traceId;
            await _next(context);
        }
    }
}
