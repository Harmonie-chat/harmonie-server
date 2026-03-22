using System.Diagnostics;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Common;

/// <summary>
/// Decorator that adds start/success/failure logging with UserId context
/// around an <see cref="IAuthenticatedHandler{TRequest, TResponse}"/>.
/// </summary>
public sealed class AuthenticatedLoggingHandlerDecorator<TRequest, TResponse>
    : IAuthenticatedHandler<TRequest, TResponse>
{
    private readonly IAuthenticatedHandler<TRequest, TResponse> _inner;
    private readonly ILogger<AuthenticatedLoggingHandlerDecorator<TRequest, TResponse>> _logger;
    private readonly string _handlerName;

    public AuthenticatedLoggingHandlerDecorator(
        IAuthenticatedHandler<TRequest, TResponse> inner,
        ILogger<AuthenticatedLoggingHandlerDecorator<TRequest, TResponse>> logger)
    {
        _inner = inner;
        _logger = logger;
        _handlerName = inner.GetType().Name;
    }

    public async Task<ApplicationResponse<TResponse>> HandleAsync(
        TRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "{Handler} started. UserId={UserId}",
            _handlerName,
            currentUserId);

        var stopwatch = Stopwatch.StartNew();
        var response = await _inner.HandleAsync(request, currentUserId, cancellationToken);
        stopwatch.Stop();

        if (response.Success)
        {
            _logger.LogInformation(
                "{Handler} succeeded in {ElapsedMs}ms. UserId={UserId}",
                _handlerName,
                stopwatch.ElapsedMilliseconds,
                currentUserId);
        }
        else
        {
            _logger.LogWarning(
                "{Handler} failed in {ElapsedMs}ms. UserId={UserId}, Error={ErrorCode}: {ErrorDetail}",
                _handlerName,
                stopwatch.ElapsedMilliseconds,
                currentUserId,
                response.Error?.Code,
                response.Error?.Detail);
        }

        return response;
    }
}
