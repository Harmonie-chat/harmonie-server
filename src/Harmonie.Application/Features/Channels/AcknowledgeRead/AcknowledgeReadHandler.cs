using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Messages;
using Harmonie.Domain.Entities.Messages;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Channels.AcknowledgeRead;

public sealed record AcknowledgeChannelReadInput(GuildChannelId ChannelId, MessageId? MessageId);

public sealed class AcknowledgeReadHandler : IAuthenticatedHandler<AcknowledgeChannelReadInput, bool>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IChannelReadStateRepository _channelReadStateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AcknowledgeReadHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository messageRepository,
        IChannelReadStateRepository channelReadStateRepository,
        IUnitOfWork unitOfWork)
    {
        _guildChannelRepository = guildChannelRepository;
        _messageRepository = messageRepository;
        _channelReadStateRepository = channelReadStateRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        AcknowledgeChannelReadInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(request.ChannelId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Read acknowledgement is only available in text channels");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        MessageId resolvedMessageId;

        if (request.MessageId is not null)
        {
            var message = await _messageRepository.GetByIdAsync(request.MessageId, cancellationToken);
            if (message is null || message.ChannelId != request.ChannelId)
            {
                return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Message.NotFound,
                    "Message was not found in this channel");
            }

            resolvedMessageId = request.MessageId;
        }
        else
        {
            var latestMessageId = await _messageRepository.GetLatestChannelMessageIdAsync(request.ChannelId, cancellationToken);
            if (latestMessageId is null)
            {
                return ApplicationResponse<bool>.Ok(true);
            }

            resolvedMessageId = latestMessageId;
        }

        var state = MessageReadState.CreateForChannel(currentUserId, request.ChannelId, resolvedMessageId);
        if (state.IsFailure || state.Value is null)
        {
            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                state.Error ?? "Invalid read state");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _channelReadStateRepository.UpsertAsync(state.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ApplicationResponse<bool>.Ok(true);
    }
}
