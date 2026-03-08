namespace Harmonie.Application.Features.Users.SearchUsers;

public sealed class SearchUsersRequest
{
    public string? Q { get; init; }

    public string? GuildId { get; init; }

    public int? Limit { get; init; }
}
