using Harmonie.Application.Common;
using Harmonie.Application.Features.Uploads.DeleteFile;
using Harmonie.Application.Features.Uploads.DownloadFile;
using Harmonie.Application.Features.Uploads.UploadFile;
using Harmonie.Domain.ValueObjects.Uploads;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonie.Application.Registration;

public static class UploadRegistration
{
    public static IServiceCollection AddUploadHandlers(this IServiceCollection services)
    {
        services.AddAuthenticatedHandler<UploadFileInput, UploadFileResponse, UploadFileHandler>();
        services.AddAuthenticatedHandler<UploadedFileId, DownloadFileResult, DownloadFileHandler>();
        services.AddAuthenticatedHandler<DeleteFileInput, bool, DeleteFileHandler>();

        return services;
    }
}
