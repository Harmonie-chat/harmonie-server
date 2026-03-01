using Harmonie.Domain.Enums;

namespace Harmonie.Application.Features.Guilds.CreateChannel;

public enum ChannelTypeInput
{
    Text = 1,
    Voice = 2
}

public static class ChannelTypeInputExtensions
{
    public static GuildChannelType ToDomain(this ChannelTypeInput input) => input switch
    {
        ChannelTypeInput.Text  => GuildChannelType.Text,
        ChannelTypeInput.Voice => GuildChannelType.Voice,
        _                      => throw new InvalidOperationException($"Unhandled ChannelTypeInput: {input}")
    };
}
