namespace Harmonie.Application.Common.Messages;

public sealed record ReactionUserDto(
    Guid UserId,
    string Username,
    string? DisplayName);

public sealed record MessageReactionDto(
    string Emoji,
    int Count,
    bool ReactedByMe,
    IReadOnlyList<ReactionUserDto> Users);
