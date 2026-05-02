using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.GetReactionUsers;

public sealed record GetChannelReactionUsersInput(
    GuildChannelId ChannelId,
    MessageId MessageId,
    string Emoji,
    string? Cursor = null,
    int? Limit = null);

public sealed class GetReactionUsersHandler : IAuthenticatedHandler<GetChannelReactionUsersInput, GetReactionUsersResponse>
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMessageReactionRepository _reactionRepository;

    public GetReactionUsersHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository messageRepository,
        IMessageReactionRepository reactionRepository)
    {
        _guildChannelRepository = guildChannelRepository;
        _messageRepository = messageRepository;
        _reactionRepository = reactionRepository;
    }

    public async Task<ApplicationResponse<GetReactionUsersResponse>> HandleAsync(
        GetChannelReactionUsersInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        ReactionUsersCursor? cursor = null;
        if (request.Cursor is not null)
        {
            if (!ReactionUsersCursorCodec.TryParse(request.Cursor, out var parsedCursor) || parsedCursor is null)
            {
                return ApplicationResponse<GetReactionUsersResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    "Request validation failed",
                    EndpointExtensions.SingleValidationError(
                        nameof(request.Cursor),
                        ApplicationErrorCodes.Validation.InvalidFormat,
                        "Cursor is invalid"));
            }

            cursor = parsedCursor;
        }

        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request.ChannelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<GetReactionUsersResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            return ApplicationResponse<GetReactionUsersResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Reactions can only be viewed in text channels");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<GetReactionUsersResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var message = await _messageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        var messageChannelId = message?.ChannelId;
        if (message is null || messageChannelId is null || messageChannelId != request.ChannelId)
        {
            return ApplicationResponse<GetReactionUsersResponse>.Fail(
                ApplicationErrorCodes.Reaction.MessageNotFound,
                "Message was not found");
        }

        var page = await _reactionRepository.GetReactionUsersAsync(
            request.MessageId,
            request.Emoji,
            limit,
            cursor,
            cancellationToken);

        var users = page.Users
            .Select(u => new ReactionUserDto(u.UserId, u.Username, u.DisplayName))
            .ToArray();

        var payload = new GetReactionUsersResponse(
            MessageId: request.MessageId.Value,
            Emoji: request.Emoji,
            TotalCount: page.TotalCount,
            Users: users,
            NextCursor: page.NextCursor is null ? null : ReactionUsersCursorCodec.Encode(page.NextCursor));

        return ApplicationResponse<GetReactionUsersResponse>.Ok(payload);
    }
}
