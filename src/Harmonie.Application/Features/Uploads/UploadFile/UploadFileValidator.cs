using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Harmonie.Application.Features.Uploads.UploadFile;

public sealed class UploadFileValidator : AbstractValidator<UploadFileRequest>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
        "application/pdf",
        "text/plain",
        "application/zip"
    };

    public UploadFileValidator(IOptions<UploadOptions> options)
    {
        var maxFileSizeBytes = options.Value.MaxFileSizeBytes > 0
            ? options.Value.MaxFileSizeBytes
            : 25L * 1024 * 1024;

        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("File is required.")
            .Must(file => file is not null && file.Length > 0)
            .WithMessage("File is required.")
            .Must(file => file is not null && file.Length <= maxFileSizeBytes)
            .WithMessage($"File size must not exceed {maxFileSizeBytes} bytes.")
            .Must(file => file is not null && HasFileName(file))
            .WithMessage("File name is required.")
            .Must(file => file is not null && HasAllowedContentType(file))
            .WithMessage("File content type is not supported.");
    }

    private static bool HasFileName(IFormFile file)
        => !string.IsNullOrWhiteSpace(Path.GetFileName(file.FileName));

    private static bool HasAllowedContentType(IFormFile file)
        => !string.IsNullOrWhiteSpace(file.ContentType)
           && AllowedContentTypes.Contains(file.ContentType.Trim());
}
