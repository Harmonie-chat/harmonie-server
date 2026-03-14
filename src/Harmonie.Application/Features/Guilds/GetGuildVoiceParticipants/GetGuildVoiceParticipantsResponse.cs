using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;

public sealed record GetGuildVoiceParticipantsResponse(
    IReadOnlyList<GetGuildVoiceParticipantsChannelResponse> Channels);

public sealed record GetGuildVoiceParticipantsChannelResponse(
    string ChannelId,
    IReadOnlyList<GetGuildVoiceParticipantResponse> Participants);

public sealed record GetGuildVoiceParticipantResponse(
    string UserId,
    string Username,
    string? DisplayName,
    string? AvatarFileId,
    AvatarAppearanceDto? Avatar);
