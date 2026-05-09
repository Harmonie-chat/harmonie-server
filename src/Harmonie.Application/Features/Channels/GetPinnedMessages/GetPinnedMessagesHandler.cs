using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.Pins;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.GetPinnedMessages;

public sealed record GetChannelPinnedMessagesInput(GuildChannelId ChannelId, string? Before = null, int? Limit = null);

public sealed class GetPinnedMessagesHandler : IAuthenticatedHandler<GetChannelPinnedMessagesInput, GetPinnedMessagesResponse>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IPinnedMessageRepository _pinnedMessageRepository;
    private readonly PinnedMessageFetchOrchestrator _orchestrator;

    public GetPinnedMessagesHandler(
        IGuildChannelRepository guildChannelRepository,
        IPinnedMessageRepository pinnedMessageRepository,
        PinnedMessageFetchOrchestrator orchestrator)
    {
        _guildChannelRepository = guildChannelRepository;
        _pinnedMessageRepository = pinnedMessageRepository;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<GetPinnedMessagesResponse>> HandleAsync(
        GetChannelPinnedMessagesInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ChannelPinnedMessageFetchScope(
            request.ChannelId, _guildChannelRepository, _pinnedMessageRepository);

        var result = await _orchestrator.FetchAsync(
            scope, request.Before, request.Limit, currentUserId, cancellationToken);

        if (!result.Success)
            return ApplicationResponse<GetPinnedMessagesResponse>.Fail(result.Error);

        return ApplicationResponse<GetPinnedMessagesResponse>.Ok(new GetPinnedMessagesResponse(
            ChannelId: request.ChannelId.Value,
            Items: result.Data.Items,
            NextCursor: result.Data.NextCursor));
    }
}
