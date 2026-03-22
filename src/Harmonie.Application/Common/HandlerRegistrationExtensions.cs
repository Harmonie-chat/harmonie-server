using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application.Common;

/// <summary>
/// DI registration helpers that wire handler → logging decorator → interface.
/// </summary>
public static class HandlerRegistrationExtensions
{
    /// <summary>
    /// Registers an <see cref="IHandler{TRequest, TResponse}"/> with a logging decorator.
    /// Resolving <c>IHandler&lt;TReq, TRes&gt;</c> returns the decorator wrapping the concrete handler.
    /// </summary>
    public static IServiceCollection AddHandler<TRequest, TResponse, THandler>(
        this IServiceCollection services)
        where THandler : class, IHandler<TRequest, TResponse>
    {
        services.AddScoped<THandler>();
        services.AddScoped<IHandler<TRequest, TResponse>>(sp =>
            new LoggingHandlerDecorator<TRequest, TResponse>(
                sp.GetRequiredService<THandler>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<
                    LoggingHandlerDecorator<TRequest, TResponse>>>()));

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IAuthenticatedHandler{TRequest, TResponse}"/> with a logging decorator.
    /// Resolving <c>IAuthenticatedHandler&lt;TReq, TRes&gt;</c> returns the decorator wrapping the concrete handler.
    /// </summary>
    public static IServiceCollection AddAuthenticatedHandler<TRequest, TResponse, THandler>(
        this IServiceCollection services)
        where THandler : class, IAuthenticatedHandler<TRequest, TResponse>
    {
        services.AddScoped<THandler>();
        services.AddScoped<IAuthenticatedHandler<TRequest, TResponse>>(sp =>
            new AuthenticatedLoggingHandlerDecorator<TRequest, TResponse>(
                sp.GetRequiredService<THandler>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<
                    AuthenticatedLoggingHandlerDecorator<TRequest, TResponse>>>()));

        return services;
    }
}
