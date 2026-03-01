using System.Net;
using FluentValidation;
using Harmonie.Application.Common;

namespace Harmonie.API.Middleware;

public sealed class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        ApplicationError error;

        if (exception is ValidationException validationException)
        {
            var details = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            error = new ApplicationError(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                details);
        }
        else if (exception is BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest })
        {
            error = new ApplicationError(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request body contains an invalid value");
        }
        else
        {
            _logger.LogError(exception, "An unhandled exception occurred");
            error = new ApplicationError(
                ApplicationErrorCodes.Common.Unexpected,
                "An unexpected error occurred");
        }

        var statusCode = EndpointExtensions.MapStatusCode(error.Code);

        context.Response.StatusCode = (int)statusCode;

        return context.Response.WriteAsJsonAsync(error);
    }
}
