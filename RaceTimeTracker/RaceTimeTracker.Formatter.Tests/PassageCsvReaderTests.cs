using System.Text;
using RaceTimeTracker.Formatter.Application;
using RaceTimeTracker.Formatter.Domain;
using RaceTimeTracker.Formatter.Infrastructure;
using Xunit;

namespace RaceTimeTracker.Formatter.Tests;

public sealed class PassageCsvReaderTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly PassageCsvReader reader = new();

    public PassageCsvReaderTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public async Task ReadsValidPassageRecordsAndPreservesStartNumbersAsText()
    {
        string path = WriteCsv(
            """
            start_number,time_elapsed
            001,00:46:12.200
            1,00:48:15.000
            """);

        FormatterResult<IReadOnlyList<PassageRecord>> result = await reader.ReadAsync(path);

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Value,
            first =>
            {
                Assert.Equal("001", first.StartNumber);
                Assert.Equal(new TimeSpan(0, 0, 46, 12, 200), first.Elapsed);
                Assert.Equal(0, first.SourceOrder);
            },
            second =>
            {
                Assert.Equal("1", second.StartNumber);
                Assert.Equal(new TimeSpan(0, 0, 48, 15, 0), second.Elapsed);
                Assert.Equal(1, second.SourceOrder);
            });
    }

    [Fact]
    public async Task AcceptsBomCrLfAndBlankPhysicalLines()
    {
        string path = WriteCsv("\uFEFFstart_number,time_elapsed\r\n\r\n112,0:46:12.2\r\n");

        FormatterResult<IReadOnlyList<PassageRecord>> result = await reader.ReadAsync(path);

        PassageRecord passage = Assert.Single(result.Value);
        Assert.Equal("112", passage.StartNumber);
        Assert.Equal(new TimeSpan(0, 0, 46, 12, 200), passage.Elapsed);
    }

    [Theory]
    [InlineData("46:12", 0, 46, 12, 0)]
    [InlineData("46:12.2", 0, 46, 12, 200)]
    [InlineData("0:46:12", 0, 46, 12, 0)]
    [InlineData("27:46:12.123", 27, 46, 12, 123)]
    public async Task ParsesSupportedInvariantDurationShapes(
        string elapsedText,
        int hours,
        int minutes,
        int seconds,
        int milliseconds)
    {
        string path = WriteCsv(
            $"""
             start_number,time_elapsed
             112,{elapsedText}
             """);

        FormatterResult<IReadOnlyList<PassageRecord>> result = await reader.ReadAsync(path);

        PassageRecord passage = Assert.Single(result.Value);
        Assert.Equal(new TimeSpan(0, hours, minutes, seconds, milliseconds), passage.Elapsed);
    }

    [Fact]
    public async Task ParsesFractionalPrecisionToTicks()
    {
        string path = WriteCsv(
            """
            start_number,time_elapsed
            112,27:46:12.1234567
            """);

        FormatterResult<IReadOnlyList<PassageRecord>> result = await reader.ReadAsync(path);

        PassageRecord passage = Assert.Single(result.Value);
        Assert.Equal(new TimeSpan(27, 46, 12) + TimeSpan.FromTicks(1_234_567), passage.Elapsed);
    }

    [Fact]
    public async Task ParsesQuotedFieldsAndEscapedQuotes()
    {
        string path = WriteCsv(
            """
            start_number,time_elapsed
            "A, ""quoted"" runner", "0:01:02.3"
            """);

        FormatterResult<IReadOnlyList<PassageRecord>> result = await reader.ReadAsync(path);

        PassageRecord passage = Assert.Single(result.Value);
        Assert.Equal("A, \"quoted\" runner", passage.StartNumber);
        Assert.Equal(new TimeSpan(0, 0, 1, 2, 300), passage.Elapsed);
    }

    [Fact]
    public async Task HeaderOnlyInputReturnsEmptyRecordSet()
    {
        string path = WriteCsv("start_number,time_elapsed");

        FormatterResult<IReadOnlyList<PassageRecord>> result = await reader.ReadAsync(path);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Theory]
    [InlineData("number,time_elapsed\r\n112,0:01:00", 1)]
    [InlineData("start_number,time_elapsed\r\n112", 2)]
    [InlineData("start_number,time_elapsed\r\n ,0:01:00", 2)]
    [InlineData("start_number,time_elapsed\r\n112,", 2)]
    [InlineData("start_number,time_elapsed\r\n112,-0:01:00", 2)]
    [InlineData("start_number,time_elapsed\r\n112,1:60:00", 2)]
    [InlineData("start_number,time_elapsed\r\n112,1:00:60", 2)]
    [InlineData("start_number,time_elapsed\r\n112,\"0:01:00", 2)]
    public async Task InvalidCsvReturnsInputErrorWithRecordContext(string csv, int expectedRecordNumber)
    {
        string path = WriteCsv(csv);

        FormatterResult<IReadOnlyList<PassageRecord>> result = await reader.ReadAsync(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(FormatterErrorKind.InvalidInput, result.Error.Kind);
        Assert.Equal(path, result.Error.Path);
        Assert.Equal(expectedRecordNumber, result.Error.RecordNumber);
    }

    [Fact]
    public async Task UnreadableSourceReturnsSourceUnavailableError()
    {
        string path = Path.Combine(tempDirectory, "missing.csv");

        FormatterResult<IReadOnlyList<PassageRecord>> result = await reader.ReadAsync(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(FormatterErrorKind.SourceUnavailable, result.Error.Kind);
        Assert.Equal(path, result.Error.Path);
    }

    public void Dispose()
    {
        Directory.Delete(tempDirectory, recursive: true);
    }

    private string WriteCsv(string content)
    {
        string path = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }
}
