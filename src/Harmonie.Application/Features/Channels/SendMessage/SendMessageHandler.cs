using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed class SendMessageHandler
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IChannelMessageRepository _channelMessageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITextChannelNotifier _textChannelNotifier;

    public SendMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        IChannelMessageRepository channelMessageRepository,
        IUnitOfWork unitOfWork,
        ITextChannelNotifier textChannelNotifier)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _channelMessageRepository = channelMessageRepository;
        _unitOfWork = unitOfWork;
        _textChannelNotifier = textChannelNotifier;
    }

    public async Task<ApplicationResponse<SendMessageResponse>> HandleAsync(
        GuildChannelId channelId,
        SendMessageRequest request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (channelId is null)
            throw new ArgumentNullException(nameof(channelId));
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (currentUserId is null)
            throw new ArgumentNullException(nameof(currentUserId));

        var contentResult = ChannelMessageContent.Create(request.Content);
        if (contentResult.IsFailure || contentResult.Value is null)
        {
            var code = ResolveContentErrorCode(request.Content);
            return ApplicationResponse<SendMessageResponse>.Fail(
                code,
                contentResult.Error ?? "Message content is invalid");
        }

        var channel = await _guildChannelRepository.GetByIdAsync(channelId, cancellationToken);
        if (channel is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotFound,
                "Channel was not found");
        }

        if (channel.Type != GuildChannelType.Text)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.NotText,
                "Messages can only be sent to text channels");
        }

        var isMember = await _guildMemberRepository.IsMemberAsync(
            channel.GuildId,
            currentUserId,
            cancellationToken);
        if (!isMember)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Channel.AccessDenied,
                "You do not have access to this channel");
        }

        var messageResult = ChannelMessage.Create(
            channelId,
            currentUserId,
            contentResult.Value);
        if (messageResult.IsFailure || messageResult.Value is null)
        {
            return ApplicationResponse<SendMessageResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                messageResult.Error ?? "Unable to create channel message");
        }

        await using (var transaction = await _unitOfWork.BeginAsync(cancellationToken))
        {
            await _channelMessageRepository.AddAsync(messageResult.Value, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await NotifyMessageCreatedSafelyAsync(
            new TextChannelMessageCreatedNotification(
                messageResult.Value.Id,
                messageResult.Value.ChannelId,
                messageResult.Value.AuthorUserId,
                messageResult.Value.Content.Value,
                messageResult.Value.CreatedAtUtc),
            cancellationToken);

        var payload = new SendMessageResponse(
            MessageId: messageResult.Value.Id.ToString(),
            ChannelId: messageResult.Value.ChannelId.ToString(),
            AuthorUserId: messageResult.Value.AuthorUserId.ToString(),
            Content: messageResult.Value.Content.Value,
            CreatedAtUtc: messageResult.Value.CreatedAtUtc);

        return ApplicationResponse<SendMessageResponse>.Ok(payload);
    }

    private static string ResolveContentErrorCode(string? rawContent)
    {
        if (rawContent is null || rawContent.Trim().Length == 0)
            return ApplicationErrorCodes.Message.ContentEmpty;

        return rawContent.Trim().Length > ChannelMessageContent.MaxLength
            ? ApplicationErrorCodes.Message.ContentTooLong
            : ApplicationErrorCodes.Common.DomainRuleViolation;
    }

    private async Task NotifyMessageCreatedSafelyAsync(
        TextChannelMessageCreatedNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await _textChannelNotifier.NotifyMessageCreatedAsync(notification, cancellationToken);
        }
        catch
        {
            // Real-time delivery is best-effort and must not fail a successful write.
        }
    }
}
