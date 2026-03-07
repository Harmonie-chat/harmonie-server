using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    private static readonly IReadOnlyDictionary<string, string> ErrorMessages =
        new Dictionary<string, string>
        {
            [ApplicationErrorCodes.Common.ValidationFailed]           = "Request validation failed",
            [ApplicationErrorCodes.Common.DomainRuleViolation]        = "A domain rule was violated",
            [ApplicationErrorCodes.Auth.InvalidCredentials]           = "Invalid credentials",
            [ApplicationErrorCodes.Auth.InvalidRefreshToken]          = "The provided refresh token is invalid or expired",
            [ApplicationErrorCodes.Auth.RefreshTokenReuseDetected]    = "Refresh token reuse was detected",
            [ApplicationErrorCodes.Auth.UserInactive]                 = "This account is inactive",
            [ApplicationErrorCodes.Auth.DuplicateEmail]               = "This email is already registered",
            [ApplicationErrorCodes.Auth.DuplicateUsername]            = "This username is already taken",
            [ApplicationErrorCodes.Guild.NotFound]                    = "Guild not found",
            [ApplicationErrorCodes.Guild.AccessDenied]                = "You do not have access to this guild",
            [ApplicationErrorCodes.Guild.InviteForbidden]             = "You are not allowed to invite members",
            [ApplicationErrorCodes.Guild.InviteTargetNotFound]        = "The invited user was not found",
            [ApplicationErrorCodes.Guild.MemberAlreadyExists]         = "This user is already a member of this guild",
            [ApplicationErrorCodes.Guild.OwnerCannotLeave]            = "The guild owner cannot leave the guild",
            [ApplicationErrorCodes.Guild.MemberNotFound]              = "Member not found in this guild",
            [ApplicationErrorCodes.Guild.OwnerCannotBeRemoved]        = "The guild owner cannot be removed",
            [ApplicationErrorCodes.Guild.OwnerRoleCannotBeChanged]    = "The guild owner's role cannot be changed",
            [ApplicationErrorCodes.Guild.OwnerTransferToSelf]         = "Cannot transfer ownership to yourself",
            [ApplicationErrorCodes.Channel.NotFound]                  = "Channel not found",
            [ApplicationErrorCodes.Channel.NotText]                   = "This channel is not a text channel",
            [ApplicationErrorCodes.Channel.AccessDenied]              = "You do not have access to this channel",
            [ApplicationErrorCodes.Channel.NameConflict]              = "A channel with this name already exists",
            [ApplicationErrorCodes.Channel.CannotDeleteDefault]       = "The default channel cannot be deleted",
            [ApplicationErrorCodes.Message.ContentEmpty]              = "Message content cannot be empty",
            [ApplicationErrorCodes.Message.ContentTooLong]            = "Message content exceeds the maximum allowed length",
            [ApplicationErrorCodes.Message.NotFound]                  = "Message not found",
            [ApplicationErrorCodes.Message.EditForbidden]             = "You are not allowed to edit this message",
            [ApplicationErrorCodes.Message.DeleteForbidden]           = "You are not allowed to delete this message",
            [ApplicationErrorCodes.User.NotFound]                     = "User not found",
        };

    /// <summary>
    /// Registers typed <see cref="ApplicationError"/> responses for every distinct HTTP status derived
    /// from <paramref name="errorCodes"/> and injects a named OpenAPI example per error code.
    /// </summary>
    public static RouteHandlerBuilder ProducesErrors(
        this RouteHandlerBuilder builder,
        params string[] errorCodes)
    {
        var byStatus = errorCodes
            .GroupBy(code => (int)MapStatusCode(code))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var status in byStatus.Keys)
            builder = builder.Produces<ApplicationError>(status);

        return builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            if (operation?.Responses is not { } responses)
                return Task.CompletedTask;

            foreach (var (status, codes) in byStatus)
            {
                var statusKey = status.ToString();
                if (!responses.TryGetValue(statusKey, out var response) || response is null)
                    continue;

                if (response.Content is null || !response.Content.TryGetValue("application/json", out var mediaType))
                    continue;

                mediaType.Examples ??= new Dictionary<string, IOpenApiExample>();
                foreach (var code in codes)
                {
                    var msg = ErrorMessages.GetValueOrDefault(code, code);
                    mediaType.Examples[code] = new OpenApiExample
                    {
                        Value = JsonNode.Parse(JsonSerializer.Serialize(new ApplicationError(code, msg)))
                    };
                }
            }
            return Task.CompletedTask;
        });
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
