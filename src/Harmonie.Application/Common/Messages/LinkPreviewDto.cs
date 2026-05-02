namespace Harmonie.Application.Common.Messages;

public sealed record LinkPreviewDto(
    string Url,
    string? Title,
    string? Description,
    string? ImageUrl,
    string? SiteName);
