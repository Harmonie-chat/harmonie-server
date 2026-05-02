using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Features.Channels.GetReactionUsers;

public sealed record GetReactionUsersResponse(
    Guid MessageId,
    string Emoji,
    int TotalCount,
    IReadOnlyList<ReactionUserDto> Users,
    string? NextCursor);
