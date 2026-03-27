namespace Harmonie.Application.Features.Users.UpdateUserStatus;

public sealed record UpdateUserStatusResponse(
    Guid UserId,
    string Status);
