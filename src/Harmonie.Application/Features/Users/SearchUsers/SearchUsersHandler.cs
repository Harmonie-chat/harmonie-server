using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Users.SearchUsers;

public sealed class SearchUsersHandler
{
    private const int DefaultLimit = 20;

    private readonly IUserRepository _userRepository;
    private readonly IGuildRepository _guildRepository;
    private readonly ILogger<SearchUsersHandler> _logger;

    public SearchUsersHandler(
        IUserRepository userRepository,
        IGuildRepository guildRepository,
        ILogger<SearchUsersHandler> logger)
    {
        _userRepository = userRepository;
        _guildRepository = guildRepository;
        _logger = logger;
    }

    public async Task<ApplicationResponse<SearchUsersResponse>> HandleAsync(
        SearchUsersRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SearchUsers started. UserId={UserId}, HasGuildScope={HasGuildScope}, Limit={Limit}",
            currentUserId,
            request.GuildId is not null,
            request.Limit ?? DefaultLimit);

        if (request.Q is not string rawQuery || string.IsNullOrWhiteSpace(rawQuery))
        {
            return ApplicationResponse<SearchUsersResponse>.Fail(
                ApplicationErrorCodes.Common.InvalidState,
                "Request validation succeeded but search query was missing.");
        }

        GuildId? guildId = null;
        if (request.GuildId is not null)
        {
            if (!GuildId.TryParse(request.GuildId, out var parsedGuildId) || parsedGuildId is null)
            {
                return ApplicationResponse<SearchUsersResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.GuildId),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Guild ID is invalid"));
            }

            guildId = parsedGuildId;
        }

        if (guildId is not null)
        {
            var guildContext = await _guildRepository.GetWithCallerRoleAsync(guildId, currentUserId, cancellationToken);
            if (guildContext is null)
            {
                _logger.LogWarning(
                    "SearchUsers failed because guild was not found. GuildId={GuildId}, UserId={UserId}",
                    guildId,
                    currentUserId);

                return ApplicationResponse<SearchUsersResponse>.Fail(
                    ApplicationErrorCodes.Guild.NotFound,
                    "Guild was not found");
            }

            if (guildContext.CallerRole is null)
            {
                _logger.LogWarning(
                    "SearchUsers access denied for guild scope. GuildId={GuildId}, UserId={UserId}",
                    guildId,
                    currentUserId);

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

        _logger.LogInformation(
            "SearchUsers succeeded. UserId={UserId}, HasGuildScope={HasGuildScope}, ResultCount={ResultCount}",
            currentUserId,
            guildId is not null,
            users.Count);

        var payload = new SearchUsersResponse(
            users.Select(user => new SearchUsersItemResponse(
                UserId: user.UserId.ToString(),
                Username: user.Username.Value,
                DisplayName: user.DisplayName,
                AvatarUrl: user.AvatarUrl,
                Status: user.IsActive ? "Active" : "Blocked"))
            .ToArray());

        return ApplicationResponse<SearchUsersResponse>.Ok(payload);
    }
}
