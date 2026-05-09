using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.DeleteMessage;

public sealed record DeleteChannelMessageInput(GuildChannelId ChannelId, MessageId MessageId);

public sealed class DeleteMessageHandler : IAuthenticatedHandler<DeleteChannelMessageInput, bool>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly ILogger<ChannelMessageEditDeleteScope> _scopeLogger;
    private readonly MessageEditDeleteOrchestrator _orchestrator;

    public DeleteMessageHandler(
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
        DeleteChannelMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ChannelMessageEditDeleteScope(
            request.ChannelId,
            _guildChannelRepository,
            _guildMemberRepository,
            _textChannelNotifier,
            _scopeLogger);

        return await _orchestrator.DeleteAsync(
            scope,
            new MessageScope.Channel(request.ChannelId),
            request.MessageId,
            currentUserId,
            cancellationToken);
    }
}
