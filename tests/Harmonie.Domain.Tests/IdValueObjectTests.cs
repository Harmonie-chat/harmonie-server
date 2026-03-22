using FluentAssertions;
using Harmonie.Domain.ValueObjects.Channels;
using Harmonie.Domain.ValueObjects.Conversations;
using Harmonie.Domain.ValueObjects.Guilds;
using Harmonie.Domain.ValueObjects.Messages;
using Harmonie.Domain.ValueObjects.Uploads;
using Harmonie.Domain.ValueObjects.Users;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class IdValueObjectTests
{
    private static readonly Guid ValidGuid = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly string ValidGuidString = ValidGuid.ToString();

    // --- GuildId ---

    [Fact]
    public void GuildId_TryParse_WithValidGuid_ShouldSucceed()
    {
        var success = GuildId.TryParse(ValidGuidString, null, out var result);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Value.Should().Be(ValidGuid);
    }

    [Fact]
    public void GuildId_Parse_WithValidGuid_ShouldReturnInstance()
    {
        var result = GuildId.Parse(ValidGuidString, null);

        result.Value.Should().Be(ValidGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GuildId_TryParse_WithInvalidInput_ShouldFail(string? input)
    {
        var success = GuildId.TryParse(input, null, out var result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GuildId_Parse_WithInvalidInput_ShouldThrow(string? input)
    {
        var act = () => GuildId.Parse(input!, null);

        act.Should().Throw<FormatException>();
    }

    // --- GuildChannelId ---

    [Fact]
    public void GuildChannelId_TryParse_WithValidGuid_ShouldSucceed()
    {
        var success = GuildChannelId.TryParse(ValidGuidString, null, out var result);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Value.Should().Be(ValidGuid);
    }

    [Fact]
    public void GuildChannelId_Parse_WithValidGuid_ShouldReturnInstance()
    {
        var result = GuildChannelId.Parse(ValidGuidString, null);

        result.Value.Should().Be(ValidGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GuildChannelId_TryParse_WithInvalidInput_ShouldFail(string? input)
    {
        var success = GuildChannelId.TryParse(input, null, out var result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void GuildChannelId_Parse_WithInvalidInput_ShouldThrow()
    {
        var act = () => GuildChannelId.Parse("bad", null);

        act.Should().Throw<FormatException>();
    }

    // --- MessageId ---

    [Fact]
    public void MessageId_TryParse_WithValidGuid_ShouldSucceed()
    {
        var success = MessageId.TryParse(ValidGuidString, null, out var result);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Value.Should().Be(ValidGuid);
    }

    [Fact]
    public void MessageId_Parse_WithValidGuid_ShouldReturnInstance()
    {
        var result = MessageId.Parse(ValidGuidString, null);

        result.Value.Should().Be(ValidGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void MessageId_TryParse_WithInvalidInput_ShouldFail(string? input)
    {
        var success = MessageId.TryParse(input, null, out var result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void MessageId_Parse_WithInvalidInput_ShouldThrow()
    {
        var act = () => MessageId.Parse("bad", null);

        act.Should().Throw<FormatException>();
    }

    // --- ConversationId ---

    [Fact]
    public void ConversationId_TryParse_WithValidGuid_ShouldSucceed()
    {
        var success = ConversationId.TryParse(ValidGuidString, null, out var result);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Value.Should().Be(ValidGuid);
    }

    [Fact]
    public void ConversationId_Parse_WithValidGuid_ShouldReturnInstance()
    {
        var result = ConversationId.Parse(ValidGuidString, null);

        result.Value.Should().Be(ValidGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void ConversationId_TryParse_WithInvalidInput_ShouldFail(string? input)
    {
        var success = ConversationId.TryParse(input, null, out var result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void ConversationId_Parse_WithInvalidInput_ShouldThrow()
    {
        var act = () => ConversationId.Parse("bad", null);

        act.Should().Throw<FormatException>();
    }

    // --- UserId ---

    [Fact]
    public void UserId_TryParse_WithValidGuid_ShouldSucceed()
    {
        var success = UserId.TryParse(ValidGuidString, null, out var result);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Value.Should().Be(ValidGuid);
    }

    [Fact]
    public void UserId_Parse_WithValidGuid_ShouldReturnInstance()
    {
        var result = UserId.Parse(ValidGuidString, null);

        result.Value.Should().Be(ValidGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void UserId_TryParse_WithInvalidInput_ShouldFail(string? input)
    {
        var success = UserId.TryParse(input, null, out var result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void UserId_Parse_WithInvalidInput_ShouldThrow()
    {
        var act = () => UserId.Parse("bad", null);

        act.Should().Throw<FormatException>();
    }

    // --- UploadedFileId ---

    [Fact]
    public void UploadedFileId_TryParse_WithValidGuid_ShouldSucceed()
    {
        var success = UploadedFileId.TryParse(ValidGuidString, null, out var result);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Value.Should().Be(ValidGuid);
    }

    [Fact]
    public void UploadedFileId_Parse_WithValidGuid_ShouldReturnInstance()
    {
        var result = UploadedFileId.Parse(ValidGuidString, null);

        result.Value.Should().Be(ValidGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void UploadedFileId_TryParse_WithInvalidInput_ShouldFail(string? input)
    {
        var success = UploadedFileId.TryParse(input, null, out var result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void UploadedFileId_Parse_WithInvalidInput_ShouldThrow()
    {
        var act = () => UploadedFileId.Parse("bad", null);

        act.Should().Throw<FormatException>();
    }

    // --- GuildInviteId ---

    [Fact]
    public void GuildInviteId_TryParse_WithValidGuid_ShouldSucceed()
    {
        var success = GuildInviteId.TryParse(ValidGuidString, null, out var result);

        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Value.Should().Be(ValidGuid);
    }

    [Fact]
    public void GuildInviteId_Parse_WithValidGuid_ShouldReturnInstance()
    {
        var result = GuildInviteId.Parse(ValidGuidString, null);

        result.Value.Should().Be(ValidGuid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void GuildInviteId_TryParse_WithInvalidInput_ShouldFail(string? input)
    {
        var success = GuildInviteId.TryParse(input, null, out var result);

        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void GuildInviteId_Parse_WithInvalidInput_ShouldThrow()
    {
        var act = () => GuildInviteId.Parse("bad", null);

        act.Should().Throw<FormatException>();
    }

    // --- ToString roundtrip ---

    [Fact]
    public void AllIds_ToString_ShouldRoundtripThroughTryParse()
    {
        var guildId = GuildId.New();
        GuildId.TryParse(guildId.ToString(), null, out var parsedGuild).Should().BeTrue();
        parsedGuild!.Should().Be(guildId);

        var channelId = GuildChannelId.New();
        GuildChannelId.TryParse(channelId.ToString(), null, out var parsedChannel).Should().BeTrue();
        parsedChannel!.Should().Be(channelId);

        var messageId = MessageId.New();
        MessageId.TryParse(messageId.ToString(), null, out var parsedMessage).Should().BeTrue();
        parsedMessage!.Should().Be(messageId);

        var conversationId = ConversationId.New();
        ConversationId.TryParse(conversationId.ToString(), null, out var parsedConversation).Should().BeTrue();
        parsedConversation!.Should().Be(conversationId);

        var userId = UserId.New();
        UserId.TryParse(userId.ToString(), null, out var parsedUser).Should().BeTrue();
        parsedUser!.Should().Be(userId);

        var fileId = UploadedFileId.New();
        UploadedFileId.TryParse(fileId.ToString(), null, out var parsedFile).Should().BeTrue();
        parsedFile!.Should().Be(fileId);

        var inviteId = GuildInviteId.New();
        GuildInviteId.TryParse(inviteId.ToString(), null, out var parsedInvite).Should().BeTrue();
        parsedInvite!.Should().Be(inviteId);
    }
}
