using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.Reactions;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.GetReactionUsers;

public sealed record GetChannelReactionUsersInput(
    GuildChannelId ChannelId,
    MessageId MessageId,
    string Emoji,
    string? Cursor = null,
    int? Limit = null);

public sealed class GetReactionUsersHandler : IAuthenticatedHandler<GetChannelReactionUsersInput, GetReactionUsersResponse>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IReactionNotifier _reactionNotifier;
    private readonly ILogger<ChannelReactionScope> _scopeLogger;
    private readonly ReactionOrchestrator _orchestrator;

    public GetReactionUsersHandler(
        IGuildChannelRepository guildChannelRepository,
        IReactionNotifier reactionNotifier,
        ILogger<ChannelReactionScope> scopeLogger,
        ReactionOrchestrator orchestrator)
    {
        _guildChannelRepository = guildChannelRepository;
        _reactionNotifier = reactionNotifier;
        _scopeLogger = scopeLogger;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<GetReactionUsersResponse>> HandleAsync(
        GetChannelReactionUsersInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ChannelReactionScope(
            request.ChannelId, _guildChannelRepository, _reactionNotifier, _scopeLogger);

        var result = await _orchestrator.GetUsersAsync(
            scope,
            new MessageScope.Channel(request.ChannelId),
            request.MessageId,
            request.Emoji,
            request.Cursor,
            request.Limit,
            currentUserId,
            cancellationToken);

        if (!result.Success)
            return ApplicationResponse<GetReactionUsersResponse>.Fail(result.Error);

        return ApplicationResponse<GetReactionUsersResponse>.Ok(new GetReactionUsersResponse(
            result.Data.MessageId,
            result.Data.Emoji,
            result.Data.TotalCount,
            result.Data.Users,
            result.Data.NextCursor));
    }
}
