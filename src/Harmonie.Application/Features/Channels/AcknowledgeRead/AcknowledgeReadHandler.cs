using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.AcknowledgeRead;

public sealed class AcknowledgeReadHandler
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IChannelReadStateRepository _channelReadStateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AcknowledgeReadHandler> _logger;

    public AcknowledgeReadHandler(
        IGuildChannelRepository guildChannelRepository,
        IMessageRepository messageRepository,
        IChannelReadStateRepository channelReadStateRepository,
        IUnitOfWork unitOfWork,
        ILogger<AcknowledgeReadHandler> logger)
    {
        _guildChannelRepository = guildChannelRepository;
        _messageRepository = messageRepository;
        _channelReadStateRepository = channelReadStateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        GuildChannelId channelId,
        MessageId? messageId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AcknowledgeRead started. ChannelId={ChannelId}, MessageId={MessageId}, CallerId={CallerId}",
            channelId,
            messageId,
            callerId);

        var ctx = await _guildChannelRepository.GetWithCallerRoleAsync(channelId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "AcknowledgeRead failed because channel was not found. ChannelId={ChannelId}",
                channelId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (ctx.Channel.Type != GuildChannelType.Text)
        {
            _logger.LogWarning(
                "AcknowledgeRead failed because channel is not text. ChannelId={ChannelId}, ChannelType={ChannelType}",
                channelId,
                ctx.Channel.Type);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Read acknowledgement is only available in text channels");
        }

        if (ctx.CallerRole is null)
        {
            _logger.LogWarning(
                "AcknowledgeRead access denied because caller is not a member. ChannelId={ChannelId}, GuildId={GuildId}, CallerId={CallerId}",
                channelId,
                ctx.Channel.GuildId,
                callerId);

            return ApplicationResponse<bool>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        MessageId resolvedMessageId;

        if (messageId is not null)
        {
            var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);
            if (message is null || message.ChannelId != channelId)
            {
                _logger.LogWarning(
                    "AcknowledgeRead failed because message was not found. ChannelId={ChannelId}, MessageId={MessageId}",
                    channelId,
                    messageId);

                return ApplicationResponse<bool>.Fail(
                    ApplicationErrorCodes.Message.NotFound,
                    "Message was not found in this channel");
            }

            resolvedMessageId = messageId;
        }
        else
        {
            var latestMessageId = await _messageRepository.GetLatestChannelMessageIdAsync(channelId, cancellationToken);
            if (latestMessageId is null)
            {
                _logger.LogInformation(
                    "AcknowledgeRead no-op because channel has no messages. ChannelId={ChannelId}",
                    channelId);

                return ApplicationResponse<bool>.Ok(true);
            }

            resolvedMessageId = latestMessageId;
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _channelReadStateRepository.UpsertAsync(callerId, channelId, resolvedMessageId, DateTime.UtcNow, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "AcknowledgeRead succeeded. ChannelId={ChannelId}, MessageId={MessageId}, CallerId={CallerId}",
            channelId,
            resolvedMessageId,
            callerId);

        return ApplicationResponse<bool>.Ok(true);
    }
}
