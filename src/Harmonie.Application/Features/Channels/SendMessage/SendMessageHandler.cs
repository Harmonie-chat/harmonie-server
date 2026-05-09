using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Services;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.SendMessage;

public sealed record SendChannelMessageInput(GuildChannelId ChannelId, string? Content, IReadOnlyList<Guid>? AttachmentFileIds = null, Guid? ReplyToMessageId = null);

public sealed class SendMessageHandler : IAuthenticatedHandler<SendChannelMessageInput, SendMessageResponse>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly LinkPreviewResolutionService _linkPreviewService;
    private readonly ILogger<ChannelSendMessageScope> _scopeLogger;
    private readonly MessageSendOrchestrator _orchestrator;

    public SendMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        ITextChannelNotifier textChannelNotifier,
        LinkPreviewResolutionService linkPreviewService,
        ILogger<ChannelSendMessageScope> scopeLogger,
        MessageSendOrchestrator orchestrator)
    {
        _guildChannelRepository = guildChannelRepository;
        _textChannelNotifier = textChannelNotifier;
        _linkPreviewService = linkPreviewService;
        _scopeLogger = scopeLogger;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<SendMessageResponse>> HandleAsync(
        SendChannelMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ChannelSendMessageScope(
            request.ChannelId,
            _guildChannelRepository,
            _textChannelNotifier,
            _linkPreviewService,
            _scopeLogger);

        var result = await _orchestrator.SendAsync(
            scope,
            new MessageScope.Channel(request.ChannelId),
            request.Content,
            request.AttachmentFileIds,
            request.ReplyToMessageId,
            currentUserId,
            cancellationToken);

        if (!result.Success)
            return ApplicationResponse<SendMessageResponse>.Fail(result.Error);

        return ApplicationResponse<SendMessageResponse>.Ok(new SendMessageResponse(
            result.Data.MessageId,
            request.ChannelId.Value,
            result.Data.AuthorUserId,
            result.Data.Content,
            result.Data.Attachments,
            result.Data.ReplyTo,
            result.Data.CreatedAtUtc));
    }
}
