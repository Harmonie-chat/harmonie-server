using System.Net;
using System.Text.Json;
using FluentValidation;
using Harmonie.Domain.Common;
using Harmonie.Domain.Exceptions;

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
        _logger.LogError(exception, "An error occurred");

        var (statusCode, payload) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                new { errors = validationEx.Errors.Select(e => e.ErrorMessage).ToArray() }),
            InvalidPasswordException invalidPasswordEx => (
                HttpStatusCode.Unauthorized,
                new { error = invalidPasswordEx.Message }),
            UserInactiveException userInactiveEx => (
                HttpStatusCode.Forbidden,
                new { error = userInactiveEx.Message }),
            DuplicateEmailException duplicateEmailEx => (
                HttpStatusCode.Conflict,
                new { error = duplicateEmailEx.Message }),
            DuplicateUsernameException duplicateUsernameEx => (
                HttpStatusCode.Conflict,
                new { error = duplicateUsernameEx.Message }),
            DomainException domainEx => (
                HttpStatusCode.BadRequest,
                new { error = domainEx.Message }),
            _ => (
                HttpStatusCode.InternalServerError,
                new { error = "An unexpected error occurred" })
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
