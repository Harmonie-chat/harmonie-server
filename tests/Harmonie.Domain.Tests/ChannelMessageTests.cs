using FluentAssertions;
using Harmonie.Domain.Entities;
using Harmonie.Domain.ValueObjects;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class ChannelMessageTests
{
    [Fact]
    public void Create_WithValidInput_ShouldSucceed()
    {
        var contentResult = MessageContent.Create("hello channel");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var result = ChannelMessage.Create(
            GuildChannelId.New(),
            UserId.New(),
            contentResult.Value!);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DeletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Delete_WhenMessageAlreadyDeleted_ShouldFail()
    {
        var contentResult = MessageContent.Create("hello channel");
        contentResult.IsSuccess.Should().BeTrue();
        contentResult.Value.Should().NotBeNull();

        var createResult = ChannelMessage.Create(
            GuildChannelId.New(),
            UserId.New(),
            contentResult.Value!);
        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.Should().NotBeNull();

        var firstDelete = createResult.Value!.Delete();
        var secondDelete = createResult.Value.Delete();

        firstDelete.IsSuccess.Should().BeTrue();
        secondDelete.IsFailure.Should().BeTrue();
    }
}
