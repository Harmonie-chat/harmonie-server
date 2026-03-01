using FluentValidation;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace Harmonie.Application.Common;

/// <summary>
/// Extension methods for endpoint validation and response handling
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// Validate a request using FluentValidation and return a standardized error payload.
    /// </summary>
    public static async Task<ApplicationError?> ValidateAsync<TRequest>(
        this TRequest request,
        IValidator<TRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            return new ApplicationError(
                ApplicationErrorCodes.Common.ValidationFailed,
                "Request validation failed",
                errors);
        }
        
        return null;
    }

    /// <summary>
    /// Convert an application response to a standardized HTTP response.
    /// </summary>
    public static IResult ToHttpResult<T>(this ApplicationResponse<T> response)
    {
        if (response.Success)
        {
            if (response.Data is null)
            {
                var failurePayload = new ApplicationError(
                    ApplicationErrorCodes.Common.InvalidState,
                    "Operation succeeded but no payload was returned.");

                return Results.Json(failurePayload, statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Ok(response.Data);
        }

        var error = response.Error ?? new ApplicationError(
            ApplicationErrorCodes.Common.Unexpected,
            "An unexpected error occurred");

        var statusCode = (int)MapStatusCode(error.Code);
        return Results.Json(error, statusCode: statusCode);
    }

    /// <summary>
    /// Convert an application response to a standardized HTTP 201 Created response.
    /// </summary>
    public static IResult ToCreatedHttpResult<T>(
        this ApplicationResponse<T> response,
        Func<T, string> locationFactory)
    {
        if (!response.Success)
            return response.ToHttpResult();

        if (response.Data is null)
        {
            var payload = new ApplicationError(
                ApplicationErrorCodes.Common.InvalidState,
                "Operation succeeded but no payload was returned.");

            return Results.Json(payload, statusCode: StatusCodes.Status500InternalServerError);
        }

        var location = locationFactory(response.Data);
        return Results.Created(location, response.Data);
    }

    public static HttpStatusCode MapStatusCode(string errorCode)
        => errorCode switch
        {
            ApplicationErrorCodes.Common.ValidationFailed => HttpStatusCode.BadRequest,
            ApplicationErrorCodes.Common.DomainRuleViolation => HttpStatusCode.BadRequest,
            ApplicationErrorCodes.Auth.InvalidCredentials => HttpStatusCode.Unauthorized,
            ApplicationErrorCodes.Auth.InvalidRefreshToken => HttpStatusCode.Unauthorized,
            ApplicationErrorCodes.Auth.RefreshTokenReuseDetected => HttpStatusCode.Unauthorized,
            ApplicationErrorCodes.Auth.UserInactive => HttpStatusCode.Forbidden,
            ApplicationErrorCodes.Auth.DuplicateEmail => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Auth.DuplicateUsername => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Guild.NotFound => HttpStatusCode.NotFound,
            ApplicationErrorCodes.Guild.AccessDenied => HttpStatusCode.Forbidden,
            ApplicationErrorCodes.Guild.InviteForbidden => HttpStatusCode.Forbidden,
            ApplicationErrorCodes.Guild.InviteTargetNotFound => HttpStatusCode.NotFound,
            ApplicationErrorCodes.Guild.MemberAlreadyExists => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Guild.NameConflict => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Guild.OwnerCannotLeave => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Guild.MemberNotFound => HttpStatusCode.NotFound,
            ApplicationErrorCodes.Guild.OwnerCannotBeRemoved => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Guild.OwnerRoleCannotBeChanged => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Guild.OwnerTransferToSelf => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Channel.NotFound => HttpStatusCode.NotFound,
            ApplicationErrorCodes.Channel.AccessDenied => HttpStatusCode.Forbidden,
            ApplicationErrorCodes.Channel.NotText => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Channel.NameConflict => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Channel.CannotDeleteDefault => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Message.ContentEmpty => HttpStatusCode.BadRequest,
            ApplicationErrorCodes.Message.ContentTooLong => HttpStatusCode.BadRequest,
            ApplicationErrorCodes.Message.NotFound => HttpStatusCode.NotFound,
            ApplicationErrorCodes.Message.EditForbidden => HttpStatusCode.Forbidden,
            ApplicationErrorCodes.Message.DeleteForbidden => HttpStatusCode.Forbidden,
            ApplicationErrorCodes.User.NotFound => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };
}
