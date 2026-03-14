namespace Harmonie.Application.Features.Users.UpdateUserStatus;

public sealed record UpdateUserStatusResponse(
    string UserId,
    string Status);
