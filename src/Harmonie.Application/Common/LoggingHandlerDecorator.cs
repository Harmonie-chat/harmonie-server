using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Common;

/// <summary>
/// Decorator that adds start/success/failure logging around an <see cref="IHandler{TRequest, TResponse}"/>.
/// </summary>
public sealed class LoggingHandlerDecorator<TRequest, TResponse> : IHandler<TRequest, TResponse>
{
    private readonly IHandler<TRequest, TResponse> _inner;
    private readonly ILogger<LoggingHandlerDecorator<TRequest, TResponse>> _logger;
    private readonly string _handlerName;

    public LoggingHandlerDecorator(
        IHandler<TRequest, TResponse> inner,
        ILogger<LoggingHandlerDecorator<TRequest, TResponse>> logger)
    {
        _inner = inner;
        _logger = logger;
        _handlerName = inner.GetType().Name;
    }

    public async Task<ApplicationResponse<TResponse>> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{Handler} started", _handlerName);

        var stopwatch = Stopwatch.StartNew();
        var response = await _inner.HandleAsync(request, cancellationToken);
        stopwatch.Stop();

        if (response.Success)
        {
            _logger.LogInformation(
                "{Handler} succeeded in {ElapsedMs}ms",
                _handlerName,
                stopwatch.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogWarning(
                "{Handler} failed in {ElapsedMs}ms. Error={ErrorCode}: {ErrorDetail}",
                _handlerName,
                stopwatch.ElapsedMilliseconds,
                response.Error?.Code,
                response.Error?.Detail);
        }

        return response;
    }
}
