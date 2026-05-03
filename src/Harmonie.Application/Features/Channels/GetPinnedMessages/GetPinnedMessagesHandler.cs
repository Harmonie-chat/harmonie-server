using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.GetPinnedMessages;

public sealed record GetChannelPinnedMessagesInput(GuildChannelId ChannelId);

public sealed class GetPinnedMessagesHandler : IAuthenticatedHandler<GetChannelPinnedMessagesInput, GetPinnedMessagesResponse>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IPinnedMessageRepository _pinnedMessageRepository;

    public GetPinnedMessagesHandler(
        IGuildChannelRepository guildChannelRepository,
        IPinnedMessageRepository pinnedMessageRepository)
    {
        _guildChannelRepository = guildChannelRepository;
        _pinnedMessageRepository = pinnedMessageRepository;
    }

    public async Task<ApplicationResponse<GetPinnedMessagesResponse>> HandleAsync(
        GetChannelPinnedMessagesInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request.ChannelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<GetPinnedMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            return ApplicationResponse<GetPinnedMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Pinned messages can only be listed in text channels");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<GetPinnedMessagesResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var pinnedMessages = await _pinnedMessageRepository.GetPinnedMessagesAsync(
            request.ChannelId,
            currentUserId,
            cancellationToken);

        var items = pinnedMessages
            .Select(x => new GetPinnedMessagesItemResponse(
                MessageId: x.MessageId,
                AuthorUserId: x.AuthorUserId,
                Content: x.Content,
                Attachments: x.Attachments,
                Reactions: x.Reactions,
                LinkPreviews: x.LinkPreviews,
                CreatedAtUtc: x.CreatedAtUtc,
                UpdatedAtUtc: x.UpdatedAtUtc,
                PinnedByUserId: x.PinnedByUserId,
                PinnedAtUtc: x.PinnedAtUtc))
            .ToArray();

        return ApplicationResponse<GetPinnedMessagesResponse>.Ok(
            new GetPinnedMessagesResponse(
                ChannelId: request.ChannelId.Value,
                Items: items));
    }
}
