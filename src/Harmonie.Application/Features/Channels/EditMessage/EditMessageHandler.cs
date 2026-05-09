using Harmonie.Application.Common;
using Harmonie.Application.Common.Messages;
using Harmonie.Application.Features.Channels.Messages;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Users;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Channels.EditMessage;

public sealed record EditChannelMessageInput(GuildChannelId ChannelId, MessageId MessageId, string Content);

public sealed class EditMessageHandler : IAuthenticatedHandler<EditChannelMessageInput, EditMessageResponse>
{
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly ITextChannelNotifier _textChannelNotifier;
    private readonly ILogger<ChannelMessageEditDeleteScope> _scopeLogger;
    private readonly MessageEditDeleteOrchestrator _orchestrator;

    public EditMessageHandler(
        IGuildChannelRepository guildChannelRepository,
        ITextChannelNotifier textChannelNotifier,
        ILogger<ChannelMessageEditDeleteScope> scopeLogger,
        MessageEditDeleteOrchestrator orchestrator)
    {
        _guildChannelRepository = guildChannelRepository;
        _textChannelNotifier = textChannelNotifier;
        _scopeLogger = scopeLogger;
        _orchestrator = orchestrator;
    }

    public async Task<ApplicationResponse<EditMessageResponse>> HandleAsync(
        EditChannelMessageInput request,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = new ChannelMessageEditDeleteScope(
            request.ChannelId,
            _guildChannelRepository,
            _textChannelNotifier,
            _scopeLogger);

        var result = await _orchestrator.EditAsync(
            scope,
            new MessageScope.Channel(request.ChannelId),
            request.MessageId,
            request.Content,
            currentUserId,
            cancellationToken);

        if (!result.Success)
            return ApplicationResponse<EditMessageResponse>.Fail(result.Error);

        return ApplicationResponse<EditMessageResponse>.Ok(new EditMessageResponse(
            MessageId: result.Data.MessageId,
            ChannelId: request.ChannelId.Value,
            AuthorUserId: result.Data.AuthorUserId,
            Content: result.Data.Content,
            Attachments: result.Data.Attachments,
            CreatedAtUtc: result.Data.CreatedAtUtc,
            UpdatedAtUtc: result.Data.UpdatedAtUtc));
    }
}
