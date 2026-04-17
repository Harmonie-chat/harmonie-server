using System.Diagnostics;
using Serilog.Context;

namespace Harmonie.API.Middleware;

/// <summary>
/// Middleware that establishes a TraceId for every request.
/// Reads the W3C traceparent header if present, otherwise generates a new TraceId.
/// Pushes the TraceId into LogContext so Serilog automatically includes it in all log entries.
/// </summary>
public sealed class TraceIdMiddleware
{
    private const string TraceParentHeaderName = "traceparent";
    private const string TraceIdItemKey = "TraceId";
    private const string ResponseHeaderName = "X-Trace-Id";
    private readonly RequestDelegate _next;

    public TraceIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string traceId;

        var traceParent = context.Request.Headers[TraceParentHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(traceParent) && TryParseTraceParent(traceParent, out var parsedTraceId))
        {
            traceId = parsedTraceId;
        }
        else
        {
            traceId = ActivityTraceId.CreateRandom().ToHexString();
        }

        using (LogContext.PushProperty("TraceId", traceId))
        {
            context.Items[TraceIdItemKey] = traceId;
            context.Response.Headers[ResponseHeaderName] = traceId;
            await _next(context);
        }
    }

    private static bool TryParseTraceParent(string traceParent, out string traceId)
    {
        // W3C traceparent format: "00-traceId-spanId-flags"
        var parts = traceParent.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            traceId = default!;
            return false;
        }

        traceId = parts[1];
        return IsValidTraceId(traceId);
    }

    private static bool IsValidTraceId(string traceId)
    {
        if (traceId.Length != 32)
            return false;

        foreach (char c in traceId)
        {
            if (!IsHex(c))
                return false;
        }

        return true;
    }

    private static bool IsHex(char c)
    {
        return (c >= '0' && c <= '9')
            || (c >= 'a' && c <= 'f')
            || (c >= 'A' && c <= 'F');
    }
}
