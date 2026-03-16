using Harmonie.Application.Common;
using Harmonie.Application.Interfaces;
using Harmonie.Domain.Entities;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Harmonie.Application.Features.Guilds.CreateChannel;

public sealed class CreateChannelHandler
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IRealtimeGroupManager _realtimeGroupManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateChannelHandler> _logger;

    public CreateChannelHandler(
        IGuildRepository guildRepository,
        IGuildChannelRepository guildChannelRepository,
        IRealtimeGroupManager realtimeGroupManager,
        IUnitOfWork unitOfWork,
        ILogger<CreateChannelHandler> logger)
    {
        _guildRepository = guildRepository;
        _guildChannelRepository = guildChannelRepository;
        _realtimeGroupManager = realtimeGroupManager;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApplicationResponse<CreateChannelResponse>> HandleAsync(
        GuildId guildId,
        UserId callerId,
        string name,
        GuildChannelType channelType,
        int position,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "CreateChannel started. GuildId={GuildId}, CallerId={CallerId}, Name={Name}, Type={Type}",
            guildId,
            callerId,
            name,
            channelType);

        var ctx = await _guildRepository.GetWithCallerRoleAsync(guildId, callerId, cancellationToken);
        if (ctx is null)
        {
            _logger.LogWarning(
                "CreateChannel failed because guild was not found. GuildId={GuildId}",
                guildId);

            return ApplicationResponse<CreateChannelResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null || ctx.CallerRole != GuildRole.Admin)
        {
            _logger.LogWarning(
                "CreateChannel failed because caller is not an admin. GuildId={GuildId}, CallerId={CallerId}",
                guildId,
                callerId);

            return ApplicationResponse<CreateChannelResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only guild admins can create channels");
        }

        var channelResult = GuildChannel.Create(guildId, name, channelType, isDefault: false, position);
        if (channelResult.IsFailure || channelResult.Value is null)
        {
            _logger.LogWarning(
                "CreateChannel domain creation failed. GuildId={GuildId}, Reason={Reason}",
                guildId,
                channelResult.Error);

            return ApplicationResponse<CreateChannelResponse>.Fail(
                ApplicationErrorCodes.Common.DomainRuleViolation,
                channelResult.Error ?? "Channel creation failed");
        }

        var channel = channelResult.Value;

        var nameConflict = await _guildChannelRepository.ExistsByNameInGuildAsync(
            guildId,
            channel.Name,
            channel.Id,
            cancellationToken);
        if (nameConflict)
        {
            _logger.LogWarning(
                "CreateChannel failed because a channel with the same name already exists. GuildId={GuildId}, Name={Name}",
                guildId,
                channel.Name);

            return ApplicationResponse<CreateChannelResponse>.Fail(
                ApplicationErrorCodes.Channel.NameConflict,
                "A channel with this name already exists in this guild");
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);
        await _guildChannelRepository.AddAsync(channel, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "CreateChannel succeeded. GuildId={GuildId}, ChannelId={ChannelId}, CallerId={CallerId}",
            guildId,
            channel.Id,
            callerId);

        if (channel.Type == GuildChannelType.Text)
        {
            await BestEffortNotificationHelper.TryNotifyAsync(
                ct => _realtimeGroupManager.AddAllGuildMembersToChannelGroupAsync(guildId, channel.Id, ct),
                TimeSpan.FromSeconds(5),
                _logger,
                "Failed to subscribe guild {GuildId} members to channel {ChannelId} SignalR group",
                guildId,
                channel.Id);
        }

        return ApplicationResponse<CreateChannelResponse>.Ok(new CreateChannelResponse(
            ChannelId: channel.Id.ToString(),
            GuildId: guildId.ToString(),
            Name: channel.Name,
            Type: channel.Type.ToString(),
            IsDefault: channel.IsDefault,
            Position: channel.Position));
    }
}
