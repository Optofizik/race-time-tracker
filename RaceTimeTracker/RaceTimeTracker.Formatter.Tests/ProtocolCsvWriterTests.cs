using System.Text;
using RaceTimeTracker.Formatter.Application;
using RaceTimeTracker.Formatter.Domain;
using RaceTimeTracker.Formatter.Infrastructure;
using Xunit;

namespace RaceTimeTracker.Formatter.Tests;

public sealed class ProtocolCsvWriterTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly ProtocolCsvWriter writer = new();

    public ProtocolCsvWriterTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public void DeriveOutputPathBuildsFormattedCsvBesideSource()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition 6578.csv");

        string outputPath = ProtocolCsvWriter.DeriveOutputPath(sourcePath);

        Assert.Equal(Path.Combine(tempDirectory, "competition 6578_formatted.csv"), outputPath);
    }

    [Fact]
    public void FormatDurationUsesTotalHoursAndTrimsTrailingFractionalZeroes()
    {
        TimeSpan elapsed = new TimeSpan(27, 46, 12) + TimeSpan.FromTicks(1_200_000);

        string formatted = ProtocolCsvWriter.FormatDuration(elapsed);

        Assert.Equal("27:46:12.12", formatted);
    }

    [Fact]
    public void FormatDurationOmitsFractionWhenZero()
    {
        string formatted = ProtocolCsvWriter.FormatDuration(new TimeSpan(0, 48, 15));

        Assert.Equal("0:48:15", formatted);
    }

    [Fact]
    public void FormatDurationPreservesSevenFractionalDigits()
    {
        TimeSpan elapsed = new TimeSpan(1, 2, 3) + TimeSpan.FromTicks(1_234_567);

        string formatted = ProtocolCsvWriter.FormatDuration(elapsed);

        Assert.Equal("1:02:03.1234567", formatted);
    }

    [Theory]
    [InlineData("112", "112")]
    [InlineData("A,B", "\"A,B\"")]
    [InlineData("A \"quoted\" runner", "\"A \"\"quoted\"\" runner\"")]
    [InlineData("A\r\nB", "\"A\r\nB\"")]
    public void EscapeCsvAppliesStandardCsvEscaping(string value, string expected)
    {
        Assert.Equal(expected, ProtocolCsvWriter.EscapeCsv(value));
    }

    [Fact]
    public async Task WriteAsyncWritesHeaderRecordsUtf8AndCrLf()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        File.WriteAllText(sourcePath, "start_number,time_elapsed", Encoding.UTF8);

        ProtocolRecord[] records =
        [
            new("112", new TimeSpan(0, 46, 12) + TimeSpan.FromMilliseconds(200), 1),
            new("A,\"B\"", new TimeSpan(0, 48, 15), 2),
        ];

        FormatterResult<string> result = await writer.WriteAsync(sourcePath, records);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.Combine(tempDirectory, "competition_formatted.csv"), result.Value);

        byte[] bytes = File.ReadAllBytes(result.Value);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

        string output = Encoding.UTF8.GetString(bytes);
        Assert.Equal(
            "start_number,time_elapsed,place\r\n"
            + "112,0:46:12.2,1\r\n"
            + "\"A,\"\"B\"\"\",0:48:15,2\r\n",
            output);
    }

    [Fact]
    public async Task WriteAsyncCreatesHeaderOnlyOutputForNoRecords()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        File.WriteAllText(sourcePath, "start_number,time_elapsed", Encoding.UTF8);

        FormatterResult<string> result = await writer.WriteAsync(sourcePath, []);

        Assert.True(result.IsSuccess);
        Assert.Equal("start_number,time_elapsed,place\r\n", File.ReadAllText(result.Value, Encoding.UTF8));
    }

    [Fact]
    public async Task WriteAsyncReplacesExistingOutputWithCompleteNewResult()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        string outputPath = Path.Combine(tempDirectory, "competition_formatted.csv");
        File.WriteAllText(sourcePath, "start_number,time_elapsed", Encoding.UTF8);
        File.WriteAllText(outputPath, "old content", Encoding.UTF8);

        FormatterResult<string> result = await writer.WriteAsync(
            sourcePath,
            [new ProtocolRecord("112", TimeSpan.FromSeconds(1), 1)]);

        Assert.True(result.IsSuccess);
        Assert.Equal("start_number,time_elapsed,place\r\n112,0:00:01,1\r\n", File.ReadAllText(outputPath, Encoding.UTF8));
    }

    [Fact]
    public async Task WriteAsyncReturnsOutputErrorWhenDestinationDirectoryCannotBeWritten()
    {
        string sourcePath = Path.Combine(tempDirectory, "missing-directory", "competition.csv");

        FormatterResult<string> result = await writer.WriteAsync(
            sourcePath,
            [new ProtocolRecord("112", TimeSpan.FromSeconds(1), 1)]);

        Assert.False(result.IsSuccess);
        Assert.Equal(FormatterErrorKind.OutputWriteFailed, result.Error.Kind);
        Assert.Equal(Path.Combine(tempDirectory, "missing-directory", "competition_formatted.csv"), result.Error.Path);
        Assert.Empty(Directory.GetFiles(tempDirectory, "*.tmp", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        Directory.Delete(tempDirectory, recursive: true);
    }
}
