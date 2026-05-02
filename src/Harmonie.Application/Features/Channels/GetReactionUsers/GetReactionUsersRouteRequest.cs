namespace Harmonie.Application.Features.Channels.GetReactionUsers;

public sealed class GetReactionUsersRouteRequest
{
    public string? Emoji { get; init; }
    public string? Cursor { get; init; }
    public int? Limit { get; init; }
}
