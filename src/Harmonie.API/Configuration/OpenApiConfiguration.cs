namespace Harmonie.API.Configuration;

public static class OpenApiConfiguration
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new()
                {
                    Title = "Harmonie API",
                    Version = "v1",
                    Description = "Open-source, self-hosted communication platform API"
                };

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
