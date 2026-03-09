using FluentAssertions;
using Harmonie.Domain.Common;
using Xunit;

namespace Harmonie.Domain.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Bind_OnSuccessResult_ShouldChainNextOperation()
    {
        var result = Result.Success(21)
            .Bind(value => Result.Success(value * 2));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Bind_OnFailureResult_ShouldKeepOriginalFailure()
    {
        var result = Result.Failure<int>("boom")
            .Bind(value => Result.Success(value * 2));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("boom");
    }

    [Fact]
    public void Match_OnGenericResult_ShouldReturnMappedValue()
    {
        var successValue = Result.Success("harmonie")
            .Match(
                onSuccess: value => value.ToUpperInvariant(),
                onFailure: error => error);

        var failureValue = Result.Failure<string>("boom")
            .Match(
                onSuccess: value => value.ToUpperInvariant(),
                onFailure: error => error);

        successValue.Should().Be("HARMONIE");
        failureValue.Should().Be("boom");
    }

    [Fact]
    public void Match_OnNonGenericResult_ShouldReturnSelectedBranch()
    {
        var success = Result.Success()
            .Match(
                onSuccess: () => "ok",
                onFailure: error => error);

        var failure = Result.Failure("boom")
            .Match(
                onSuccess: () => "ok",
                onFailure: error => error);

        success.Should().Be("ok");
        failure.Should().Be("boom");
    }
}
