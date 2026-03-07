using System.Net;
using System.Data.Common;
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
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new ApplicationValidationError(
                        EndpointExtensions.NormalizeValidationErrorCode(e.ErrorCode),
                        e.ErrorMessage))
                        .ToArray());

            error = new ApplicationError(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                errors);
        }
        else if (exception is BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest })
        {
            error = new ApplicationError(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request body contains an invalid value");
        }
        else if (exception is DbException)
        {
            _logger.LogError(exception, "A database exception occurred");
            error = new ApplicationError(
                ApplicationErrorCodes.Common.Unexpected,
                "An unexpected server error occurred");
        }
        else
        {
            _logger.LogError(exception, "An unhandled exception occurred");
            error = new ApplicationError(
                ApplicationErrorCodes.Common.Unexpected,
                "An unexpected error occurred");
        }

        return EndpointExtensions.WriteErrorAsync(context.Response, error);
    }
}
