using Harmonie.Application.Features.Users;

namespace Harmonie.Application.Features.Guilds.GetGuildVoiceParticipants;

public sealed record GetGuildVoiceParticipantsResponse(
    IReadOnlyList<GetGuildVoiceParticipantsChannelResponse> Channels);

public sealed record GetGuildVoiceParticipantsChannelResponse(
    Guid ChannelId,
    IReadOnlyList<GetGuildVoiceParticipantResponse> Participants);

public sealed record GetGuildVoiceParticipantResponse(
    Guid UserId,
    string Username,
    string? DisplayName,
    Guid? AvatarFileId,
    AvatarAppearanceDto? Avatar);
