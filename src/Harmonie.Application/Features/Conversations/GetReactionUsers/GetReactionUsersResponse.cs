using Harmonie.Application.Common.Messages;

namespace Harmonie.Application.Features.Conversations.GetReactionUsers;

public sealed record GetReactionUsersResponse(
    Guid MessageId,
    string Emoji,
    int TotalCount,
    IReadOnlyList<ReactionUserDto> Users,
    string? NextCursor);
