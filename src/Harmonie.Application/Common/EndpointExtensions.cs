using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using System.Diagnostics;
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
                    g => g.Select(e => new ApplicationValidationError(
                        NormalizeValidationErrorCode(e.ErrorCode),
                        e.ErrorMessage))
                        .ToArray()
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

                return Results.Json(
                    EnrichError(
                        failurePayload,
                        StatusCodes.Status500InternalServerError,
                        Activity.Current?.Id),
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Results.Ok(response.Data);
        }

        var error = response.Error ?? new ApplicationError(
            ApplicationErrorCodes.Common.Unexpected,
            "An unexpected error occurred");

        var statusCode = (int)MapStatusCode(error.Code);
        return Results.Json(
            EnrichError(error, statusCode, Activity.Current?.Id),
            statusCode: statusCode);
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

            return Results.Json(
                EnrichError(
                    payload,
                    StatusCodes.Status500InternalServerError,
                    Activity.Current?.Id),
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var location = locationFactory(response.Data);
        return Results.Created(location, response.Data);
    }

    public static Task WriteErrorAsync(
        HttpResponse response,
        ApplicationError error)
    {
        var statusCode = (int)MapStatusCode(error.Code);
        response.StatusCode = statusCode;
        return response.WriteAsJsonAsync(
            EnrichError(error, statusCode, response.HttpContext.TraceIdentifier));
    }

    public static IReadOnlyDictionary<string, ApplicationValidationError[]> SingleValidationError(
        string propertyName,
        string code,
        string detail)
        => new Dictionary<string, ApplicationValidationError[]>
        {
            [propertyName] = [new(code, detail)]
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

                var responseDescription = string.Join(
                    Environment.NewLine,
                    codes.Select(code => $"- `{code}`"));

                response.Description = string.IsNullOrWhiteSpace(response.Description)
                    ? $"Possible application error codes:{Environment.NewLine}{responseDescription}"
                    : $"{response.Description}{Environment.NewLine}{Environment.NewLine}Possible application error codes:{Environment.NewLine}{responseDescription}";

                if (response.Content is null || !response.Content.TryGetValue("application/json", out var mediaType))
                {
                    continue;
                }

                mediaType.Examples ??= new Dictionary<string, IOpenApiExample>();
                foreach (var code in codes)
                {
                    var errors = code == ApplicationErrorCodes.Common.ValidationFailed
                        ? SingleValidationError(
                            "field",
                            ApplicationErrorCodes.Validation.Required,
                            "Field is required")
                        : null;

                    mediaType.Examples[code] = new OpenApiExample
                    {
                        Summary = code,
                        Value = JsonNode.Parse(JsonSerializer.Serialize(
                            EnrichError(
                                new ApplicationError(code, BuildExampleDetail(code), errors),
                                status,
                                "trace-id")))
                    };
                }
            }
            return Task.CompletedTask;
        });
    }

    private static string BuildExampleDetail(string errorCode)
    {
        var parts = errorCode.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return errorCode;

        var words = parts
            .Skip(parts[0] is "AUTH" or "COMMON" ? 1 : 0)
            .Select(static part => part.ToLowerInvariant())
            .ToArray();

        if (words.Length == 0)
            return errorCode;

        words[0] = char.ToUpperInvariant(words[0][0]) + words[0][1..];
        return string.Join(' ', words);
    }

    private static ApplicationError EnrichError(
        ApplicationError error,
        int status,
        string? traceId)
        => error with
        {
            Status = status,
            TraceId = string.IsNullOrWhiteSpace(traceId) ? error.TraceId : traceId
        };

    public static string NormalizeValidationErrorCode(string fluentValidationCode)
        => fluentValidationCode switch
        {
            "NotNullValidator" or "NotEmptyValidator" => ApplicationErrorCodes.Validation.Required,
            "EmailValidator" => ApplicationErrorCodes.Validation.Email,
            "MinimumLengthValidator" => ApplicationErrorCodes.Validation.MinLength,
            "MaximumLengthValidator" => ApplicationErrorCodes.Validation.MaxLength,
            "InclusiveBetweenValidator"
                or "ExclusiveBetweenValidator"
                or "GreaterThanValidator"
                or "GreaterThanOrEqualValidator"
                or "LessThanValidator"
                or "LessThanOrEqualValidator" => ApplicationErrorCodes.Validation.OutOfRange,
            "RegularExpressionValidator" => ApplicationErrorCodes.Validation.InvalidFormat,
            "PredicateValidator" => ApplicationErrorCodes.Validation.Invalid,
            _ => ApplicationErrorCodes.Validation.Invalid
        };

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
            ApplicationErrorCodes.Channel.NotVoice => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Channel.NameConflict => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Channel.CannotDeleteDefault => HttpStatusCode.Conflict,
            ApplicationErrorCodes.Message.ContentEmpty => HttpStatusCode.BadRequest,
            ApplicationErrorCodes.Message.ContentTooLong => HttpStatusCode.BadRequest,
            ApplicationErrorCodes.Message.NotFound => HttpStatusCode.NotFound,
            ApplicationErrorCodes.Message.EditForbidden => HttpStatusCode.Forbidden,
            ApplicationErrorCodes.Message.DeleteForbidden => HttpStatusCode.Forbidden,
            ApplicationErrorCodes.User.NotFound => HttpStatusCode.NotFound,
            ApplicationErrorCodes.Conversation.NotFound => HttpStatusCode.NotFound,
            ApplicationErrorCodes.Conversation.CannotOpenSelf => HttpStatusCode.BadRequest,
            ApplicationErrorCodes.Conversation.AccessDenied => HttpStatusCode.Forbidden,
            _ => HttpStatusCode.InternalServerError
        };
}
