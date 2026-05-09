using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.DeleteMessageAttachment;

public sealed record DeleteChannelMessageAttachmentInput(GuildChannelId ChannelId, MessageId MessageId, UploadedFileId AttachmentId);

public sealed class DeleteMessageAttachmentHandler : IAuthenticatedHandler<DeleteChannelMessageAttachmentInput, bool>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly ILogger<ChannelMessageEditDeleteScope> _scopeLogger;
    private readonly MessageEditDeleteOrchestrator _orchestrator;

    public DeleteMessageAttachmentHandler(
        IGuildChannelRepository guildChannelRepository,
        IGuildMemberRepository guildMemberRepository,
        ITextChannelNotifier textChannelNotifier,
        ILogger<ChannelMessageEditDeleteScope> scopeLogger,
        MessageEditDeleteOrchestrator orchestrator)
    {
        _guildChannelRepository = guildChannelRepository;
        _guildMemberRepository = guildMemberRepository;
        _textChannelNotifier = textChannelNotifier;
        _scopeLogger = scopeLogger;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<bool>> HandleAsync(
        DeleteChannelMessageAttachmentInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ChannelMessageEditDeleteScope(
            request.ChannelId,
            _guildChannelRepository,
            _guildMemberRepository,
            _textChannelNotifier,
            _scopeLogger);

        return await _orchestrator.DeleteAttachmentAsync(
            scope,
            new MessageScope.Channel(request.ChannelId),
            request.MessageId,
            request.AttachmentId,
            currentUserId,
            cancellationToken);
    }
}
