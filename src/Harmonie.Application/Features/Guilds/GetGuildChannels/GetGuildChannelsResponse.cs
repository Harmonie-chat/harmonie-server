namespace Harmonie.Application.Features.Guilds.GetGuildChannels;

public sealed record GetGuildChannelsResponse(
    Guid GuildId,
    IReadOnlyList<GetGuildChannelsItemResponse> Channels);

public sealed record GetGuildChannelsItemResponse(
    Guid ChannelId,
    string Name,
    string Type,
    bool IsDefault,
    int Position,
    IReadOnlyList<GetGuildChannelsVoiceParticipantResponse>? CurrentParticipants,
    bool HasUnread);

public sealed record GetGuildChannelsVoiceParticipantResponse(
    Guid UserId,
    string? Username,
    string? DisplayName,
    Guid? AvatarFileId,
    string? AvatarColor,
    string? AvatarIcon,
    string? AvatarBg);
