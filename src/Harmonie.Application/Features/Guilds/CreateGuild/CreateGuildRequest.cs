namespace Harmonie.Application.Features.Guilds.CreateGuild;

public sealed record CreateGuildRequest(
    string Name,
    string? IconFileId = null,
    CreateGuildIconRequest? Icon = null);

public sealed record CreateGuildIconRequest(
    string? Color = null,
    string? Name = null,
    string? Bg = null);
