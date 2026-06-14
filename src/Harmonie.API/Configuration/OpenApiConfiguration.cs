using Microsoft.OpenApi;

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

                document.Tags ??= new HashSet<OpenApiTag>();
                var notificationTag = document.Tags.FirstOrDefault(tag => tag.Name == "Notifications");
                var notificationDescription = "Web Push registration plus outbound notification payload contracts. " +
                    "Push delivery is asynchronous through Harmonie.Workers; clients cannot trigger delivery directly through an HTTP send endpoint.";

                if (notificationTag is null)
                {
                    document.Tags.Add(new OpenApiTag
                    {
                        Name = "Notifications",
                        Description = notificationDescription
                    });
                }
                else
                {
                    notificationTag.Description = notificationDescription;
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
