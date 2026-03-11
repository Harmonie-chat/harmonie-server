using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Common;

public static class UploadedFileUrl
{
    private const string FilesPathPrefix = "/api/files/";

    public static bool IsValid(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (TryParseFileId(url, out _))
            return true;

        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
            return string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    public static bool TryParseFileId(string? url, out UploadedFileId? uploadedFileId)
    {
        uploadedFileId = null;

        if (!TryGetPath(url, out var path))
            return false;

        var normalizedPath = path.StartsWith("/", StringComparison.Ordinal)
            ? path
            : "/" + path;

        if (!normalizedPath.StartsWith(FilesPathPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var fileIdValue = normalizedPath[FilesPathPrefix.Length..];
        var separatorIndex = fileIdValue.IndexOfAny(['/', '?', '#']);
        if (separatorIndex >= 0)
            fileIdValue = fileIdValue[..separatorIndex];

        return UploadedFileId.TryParse(fileIdValue, out uploadedFileId);
    }

    private static bool TryGetPath(string? url, out string path)
    {
        path = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            path = absoluteUri.AbsolutePath;
            return true;
        }

        if (Uri.TryCreate(url, UriKind.Relative, out var relativeUri))
        {
            path = relativeUri.OriginalString;
            return true;
        }

        return false;
    }
}
