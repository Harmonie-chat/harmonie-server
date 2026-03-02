using System.Net;
using System.Data.Common;
using FluentValidation;
using Harmonie.Application.Common;
using Microsoft.AspNetCore.Mvc;

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
        else if (exception is DbException)
        {
            _logger.LogError(exception, "A database exception occurred");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected server error occurred."
            };

            return context.Response.WriteAsJsonAsync(problem);
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
