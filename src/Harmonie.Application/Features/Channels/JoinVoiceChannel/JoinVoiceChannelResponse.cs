namespace Harmonie.Application.Features.Channels.JoinVoiceChannel;

public sealed record JoinVoiceChannelResponse(
    string Token,
    string Url,
    string RoomName);
