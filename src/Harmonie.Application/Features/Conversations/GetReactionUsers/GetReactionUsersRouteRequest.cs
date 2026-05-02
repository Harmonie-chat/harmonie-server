namespace Harmonie.Application.Features.Conversations.GetReactionUsers;

public sealed class GetReactionUsersRouteRequest
{
    public string? Emoji { get; init; }
    public string? Cursor { get; init; }
    public int? Limit { get; init; }
}
