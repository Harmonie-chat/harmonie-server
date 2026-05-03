using Harmonie.Application.Common;
using Harmonie.Application.Interfaces.Channels;
using Harmonie.Application.Interfaces.Common;
using Harmonie.Application.Interfaces.Guilds;
using Harmonie.Domain.Enums;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Users;

namespace Harmonie.Application.Features.Guilds.ReorderChannels;

public sealed record ReorderChannelsInput(GuildId GuildId, IReadOnlyList<ReorderChannelsItemRequest> Channels);

public sealed class ReorderChannelsHandler : IAuthenticatedHandler<ReorderChannelsInput, ReorderChannelsResponse>
{
    private readonly IGuildRepository _guildRepository;
    private readonly IGuildChannelRepository _guildChannelRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuildNotifier _guildNotifier;

    public ReorderChannelsHandler(
        IGuildRepository guildRepository,
        IGuildChannelRepository guildChannelRepository,
        IUnitOfWork unitOfWork,
        IGuildNotifier guildNotifier)
    {
        _guildRepository = guildRepository;
        _guildChannelRepository = guildChannelRepository;
        _unitOfWork = unitOfWork;
        _guildNotifier = guildNotifier;
    }

    public async Task<ApplicationResponse<ReorderChannelsResponse>> HandleAsync(
        ReorderChannelsInput input,
        UserId currentUserId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _guildRepository.GetWithCallerRoleAsync(input.GuildId, currentUserId, cancellationToken);
        if (ctx is null)
        {
            return ApplicationResponse<ReorderChannelsResponse>.Fail(
                ApplicationErrorCodes.Guild.NotFound,
                "Guild was not found");
        }

        if (ctx.CallerRole is null)
        {
            return ApplicationResponse<ReorderChannelsResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "You do not have access to this guild");
        }

        if (ctx.CallerRole != GuildRole.Admin)
        {
            return ApplicationResponse<ReorderChannelsResponse>.Fail(
                ApplicationErrorCodes.Guild.AccessDenied,
                "Only guild admins can reorder channels");
        }

        var channels = await _guildChannelRepository.GetByGuildIdAsync(input.GuildId, cancellationToken);

        var channelMap = channels.ToDictionary(c => c.Id);

        var parsedItems = new List<(GuildChannelId Id, int Position)>(input.Channels.Count);
        var seenIds = new HashSet<GuildChannelId>();

        foreach (var item in input.Channels)
        {
            var parsedId = GuildChannelId.From(item.ChannelId);

            if (!channelMap.ContainsKey(parsedId))
            {
                return ApplicationResponse<ReorderChannelsResponse>.Fail(
                    ApplicationErrorCodes.Channel.NotFound,
                    $"Channel '{item.ChannelId}' was not found in this guild");
            }

            if (!seenIds.Add(parsedId))
            {
                return ApplicationResponse<ReorderChannelsResponse>.Fail(
                    ApplicationErrorCodes.Common.ValidationFailed,
                    $"Channel '{item.ChannelId}' appears more than once in the request");
            }

            parsedItems.Add((parsedId, item.Position));
        }

        await using var transaction = await _unitOfWork.BeginAsync(cancellationToken);

        foreach (var (id, position) in parsedItems)
        {
            var channel = channelMap[id];

            var result = channel.UpdatePosition(position);
            if (result.IsFailure)
            {
                return ApplicationResponse<ReorderChannelsResponse>.Fail(
                    ApplicationErrorCodes.Common.DomainRuleViolation,
                    result.Error ?? "Channel position update failed");
            }

            await _guildChannelRepository.UpdateAsync(channel, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var updatedChannels = await _guildChannelRepository.GetByGuildIdAsync(input.GuildId, cancellationToken);

        var payload = new ReorderChannelsResponse(
            GuildId: input.GuildId.Value,
            Channels: updatedChannels.Select(c => new ReorderChannelsItemResponse(
                ChannelId: c.Id.Value,
                Name: c.Name,
                Type: c.Type.ToString(),
                IsDefault: c.IsDefault,
                Position: c.Position)).ToArray());

        await _guildNotifier.NotifyChannelsReorderedAsync(
            new ChannelsReorderedNotification(
                GuildId: input.GuildId,
                GuildName: ctx.Guild.Name.Value,
                Channels: updatedChannels
                    .Select(c => new ChannelPositionItem(c.Id, c.Position))
                    .ToArray()),
            cancellationToken);

        return ApplicationResponse<ReorderChannelsResponse>.Ok(payload);
    }
}
