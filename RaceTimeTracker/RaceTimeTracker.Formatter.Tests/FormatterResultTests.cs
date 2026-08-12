using RaceTimeTracker.Formatter.Application;
using Xunit;

namespace RaceTimeTracker.Formatter.Tests;

public sealed class FormatterResultTests
{
    [Fact]
    public void SuccessResultExposesValue()
    {
        FormatterResult<string> result = FormatterResult<string>.Success("created.csv");

        Assert.True(result.IsSuccess);
        Assert.Equal("created.csv", result.Value);
    }

    [Fact]
    public void FailureResultExposesTypedError()
    {
        FormatterError error = new(FormatterErrorKind.InvalidInput, "Invalid record.", "competition.csv", 2);

        FormatterResult<string> result = FormatterResult<string>.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(FormatterErrorKind.InvalidInput, result.Error.Kind);
        Assert.Equal("competition.csv", result.Error.Path);
        Assert.Equal(2, result.Error.RecordNumber);
    }
}
