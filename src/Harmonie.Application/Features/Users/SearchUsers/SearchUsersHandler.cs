using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Application.Interfaces.Users;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Users.SearchUsers;

public sealed class SearchUsersHandler
    : IAuthenticatedHandler<SearchUsersRequest, SearchUsersResponse>
{
    private const int DefaultLimit = 20;

    private readonly IUserRepository _userRepository;
    private readonly IGuildRepository _guildRepository;

    public SearchUsersHandler(
        IUserRepository userRepository,
        IGuildRepository guildRepository)
    {
        _userRepository = userRepository;
        _guildRepository = guildRepository;
    }

    public async Task<ApplicationResponse<SearchUsersResponse>> HandleAsync(
        SearchUsersRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.Q is not string rawQuery || string.IsNullOrWhiteSpace(rawQuery))
        {
            return ApplicationResponse<SearchUsersResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Request validation succeeded but search query was missing.");
        }

        var guildId = request.GuildId.HasValue ? GuildId.From(request.GuildId.Value) : null;

        if (guildId is not null)
        {
            var guildContext = await _guildRepository.GetWithCallerRoleAsync(guildId, currentUserId, cancellationToken);
            if (guildContext is null)
            {
                return ApplicationResponse<SearchUsersResponse>.Fail(
                    ApplicationErrorCodes.Guild.NotFound,
                    "Guild was not found");
            }

            if (guildContext.CallerRole is null)
            {
                return ApplicationResponse<SearchUsersResponse>.Fail(
                    ApplicationErrorCodes.Guild.AccessDenied,
                    "You do not have access to this guild");
            }
        }

        var limit = request.Limit ?? DefaultLimit;
        var users = await _userRepository.SearchUsersAsync(
            new SearchUsersQuery(
                SearchText: rawQuery.Trim(),
                GuildId: guildId,
                Limit: limit),
            cancellationToken);

        var payload = new SearchUsersResponse(
            users.Select(user =>
            {
                var avatar = user.AvatarColor is not null || user.AvatarIcon is not null || user.AvatarBg is not null
                    ? new AvatarAppearanceDto(user.AvatarColor, user.AvatarIcon, user.AvatarBg)
                    : null;

                return new SearchUsersItemResponse(
                    UserId: user.UserId.Value,
                    Username: user.Username.Value,
                    DisplayName: user.DisplayName,
                    AvatarFileId: user.AvatarFileId?.Value,
                    Avatar: avatar,
                    Bio: user.Bio,
                    Status: user.IsActive ? "Active" : "Blocked");
            })
            .ToArray());

        return ApplicationResponse<SearchUsersResponse>.Ok(payload);
    }
}
