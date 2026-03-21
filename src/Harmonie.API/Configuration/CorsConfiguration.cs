namespace Harmonie.API.Configuration;

public static class CorsConfiguration
{
    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var corsSettings = configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
        var allowedOrigins = corsSettings.AllowedOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        services.AddCors(options =>
        {
            options.AddPolicy("ApiCors", policy =>
            {
                if (environment.IsDevelopment()
                    && allowedOrigins.Contains("*", StringComparer.Ordinal))
                {
                    policy.SetIsOriginAllowed(_ => true)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                    return;
                }

                var configuredOrigins = allowedOrigins
                    .Where(origin => !string.Equals(origin, "*", StringComparison.Ordinal))
                    .ToArray();

                if (configuredOrigins.Length > 0)
                {
                    policy.WithOrigins(configuredOrigins)
                        .AllowCredentials()
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
                        .WithHeaders("Authorization", "Content-Type");
                }
            });
        });

        return services;
    }
}
