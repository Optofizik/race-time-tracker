using System.Text;
using RaceTimeTracker.Formatter.Application;
using RaceTimeTracker.Formatter.Domain;
using RaceTimeTracker.Formatter.Infrastructure;
using Xunit;

namespace RaceTimeTracker.Formatter.Tests;

public sealed class FormatterConsoleRunnerTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), $"formatter-cli-{Guid.NewGuid():N}");

    public FormatterConsoleRunnerTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public async Task NoArgumentReturnsInvalidUsageExitCodeAndWritesStandardError()
    {
        RunnerResult result = await RunWithRealApplicationAsync([]);

        Assert.Equal(FormatterExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains("Usage:", result.StandardError, StringComparison.Ordinal);
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public async Task ExtraArgumentReturnsInvalidUsageExitCode()
    {
        RunnerResult result = await RunWithRealApplicationAsync(["one.csv", "two.csv"]);

        Assert.Equal(FormatterExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains("Usage:", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingSourceReturnsSourceUnavailableExitCode()
    {
        string missingPath = Path.Combine(tempDirectory, "missing.csv");

        RunnerResult result = await RunWithRealApplicationAsync([missingPath]);

        Assert.Equal(FormatterExitCodes.SourceUnavailable, result.ExitCode);
        Assert.Contains("not found or is inaccessible", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DirectorySourceReturnsSourceUnavailableExitCode()
    {
        RunnerResult result = await RunWithRealApplicationAsync([tempDirectory]);

        Assert.Equal(FormatterExitCodes.SourceUnavailable, result.ExitCode);
        Assert.Contains("is a directory", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingNonCsvSourceReturnsInvalidUsageExitCode()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.txt");
        File.WriteAllText(sourcePath, "start_number,time_elapsed", Encoding.UTF8);

        RunnerResult result = await RunWithRealApplicationAsync([sourcePath]);

        Assert.Equal(FormatterExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains(".csv extension", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCsvDataReturnsInvalidInputExitCodeWithoutStackTrace()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        File.WriteAllText(
            sourcePath,
            "start_number,time_elapsed\r\n112,not-a-duration\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        RunnerResult result = await RunWithRealApplicationAsync([sourcePath]);

        Assert.Equal(FormatterExitCodes.InvalidInput, result.ExitCode);
        Assert.Contains("not a supported non-negative duration", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutputWriteFailureReturnsOutputWriteExitCode()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        File.WriteAllText(sourcePath, "start_number,time_elapsed", Encoding.UTF8);

        FormatterApplication application = new(
            new StaticPassageCsvReader([new PassageRecord("112", TimeSpan.FromSeconds(1), 0)]),
            new ProtocolFormatter(),
            new FailingProtocolCsvWriter(FormatterErrorKind.OutputWriteFailed, "writer failed"));

        RunnerResult result = await RunAsync(application, [sourcePath]);

        Assert.Equal(FormatterExitCodes.OutputWriteFailed, result.ExitCode);
        Assert.Contains("writer failed", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnexpectedApplicationFailureReturnsUnexpectedExitCodeWithoutStackTrace()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        File.WriteAllText(sourcePath, "start_number,time_elapsed", Encoding.UTF8);

        FormatterApplication application = new(
            new ThrowingPassageCsvReader(),
            new ProtocolFormatter(),
            new ProtocolCsvWriter());

        RunnerResult result = await RunAsync(application, [sourcePath]);

        Assert.Equal(FormatterExitCodes.UnexpectedFailure, result.ExitCode);
        Assert.Equal($"Unexpected formatter failure.{Environment.NewLine}", result.StandardError);
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public async Task ValidRelativeCsvPathCreatesProtocolAndPrintsSuccess()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        File.WriteAllText(
            sourcePath,
            "start_number,time_elapsed\r\n112,00:46:12.200\r\n103,00:48:15.000\r\n112,01:02:41.350\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        string relativeSourcePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), sourcePath);

        RunnerResult result = await RunWithRealApplicationAsync([relativeSourcePath]);

        string outputPath = Path.Combine(tempDirectory, "competition_formatted.csv");
        Assert.Equal(FormatterExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Contains(outputPath, result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("2 ranked runner(s)", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(
            "start_number,time_elapsed,place\r\n"
            + "103,0:48:15,1\r\n"
            + "112,1:02:41.35,2\r\n",
            File.ReadAllText(outputPath, Encoding.UTF8));
    }

    public void Dispose()
    {
        Directory.Delete(tempDirectory, recursive: true);
    }

    private static Task<RunnerResult> RunWithRealApplicationAsync(string[] args)
    {
        FormatterApplication application = new(
            new PassageCsvReader(),
            new ProtocolFormatter(),
            new ProtocolCsvWriter());

        return RunAsync(application, args);
    }

    private static async Task<RunnerResult> RunAsync(FormatterApplication application, string[] args)
    {
        StringWriter standardOutput = new();
        StringWriter standardError = new();
        FormatterConsoleRunner runner = new(application, standardOutput, standardError);

        int exitCode = await runner.RunAsync(args);

        return new RunnerResult(exitCode, standardOutput.ToString(), standardError.ToString());
    }

    private sealed record RunnerResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class StaticPassageCsvReader : IPassageCsvReader
    {
        private readonly IReadOnlyList<PassageRecord> passages;

        public StaticPassageCsvReader(IReadOnlyList<PassageRecord> passages)
        {
            this.passages = passages;
        }

        public Task<FormatterResult<IReadOnlyList<PassageRecord>>> ReadAsync(
            string sourcePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(FormatterResult<IReadOnlyList<PassageRecord>>.Success(passages));
        }
    }

    private sealed class FailingProtocolCsvWriter : IProtocolCsvWriter
    {
        private readonly FormatterErrorKind kind;
        private readonly string message;

        public FailingProtocolCsvWriter(FormatterErrorKind kind, string message)
        {
            this.kind = kind;
            this.message = message;
        }

        public Task<FormatterResult<string>> WriteAsync(
            string sourcePath,
            IReadOnlyList<ProtocolRecord> protocolRecords,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                FormatterResult<string>.Failure(new FormatterError(kind, message, sourcePath)));
        }
    }

    private sealed class ThrowingPassageCsvReader : IPassageCsvReader
    {
        public Task<FormatterResult<IReadOnlyList<PassageRecord>>> ReadAsync(
            string sourcePath,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
