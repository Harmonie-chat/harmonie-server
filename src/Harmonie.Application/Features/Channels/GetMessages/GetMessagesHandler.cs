using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.GetMessages;

public sealed record GetChannelMessagesInput(GuildChannelId ChannelId, string? Before = null, int? Limit = null);

public sealed class GetMessagesHandler : IAuthenticatedHandler<GetChannelMessagesInput, GetMessagesResponse>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessagePaginationRepository _paginationRepository;
    private readonly MessageFetchOrchestrator _orchestrator;

    public GetMessagesHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessagePaginationRepository paginationRepository,
        MessageFetchOrchestrator orchestrator)
    {
        _guildChannelRepository = guildChannelRepository;
        _paginationRepository = paginationRepository;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<GetMessagesResponse>> HandleAsync(
        GetChannelMessagesInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ChannelMessagePageScope(
            request.ChannelId, _guildChannelRepository, _paginationRepository);

        var result = await _orchestrator.FetchAsync(
            scope, request.Before, request.Limit, currentUserId, cancellationToken);

        if (!result.Success)
            return ApplicationResponse<GetMessagesResponse>.Fail(result.Error);

        return ApplicationResponse<GetMessagesResponse>.Ok(new GetMessagesResponse(
            ChannelId: request.ChannelId.Value,
            Items: result.Data.Items,
            NextCursor: result.Data.NextCursor,
            LastReadMessageId: result.Data.LastReadMessageId,
            LastReadAtUtc: result.Data.LastReadAtUtc));
    }
}
