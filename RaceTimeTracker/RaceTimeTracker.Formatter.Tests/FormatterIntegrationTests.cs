using System.Text;
using Xunit;

namespace RaceTimeTracker.Formatter.Tests;

public sealed class FormatterIntegrationTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"formatter-integration-{Guid.NewGuid():N}");

    public FormatterIntegrationTests()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [Fact]
    public async Task RealCsvFileProducesCompleteFormattedProtocol()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        await File.WriteAllTextAsync(
            sourcePath,
            "start_number,time_elapsed\r\n"
            + "009,00:09:00.000\r\n"
            + "112,00:46:12.200\r\n"
            + "103,00:48:15.000\r\n"
            + "112,01:02:41.350\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        ProcessResult result = await RunFormatterProcessAsync(sourcePath);

        string outputPath = Path.Combine(tempDirectory, "competition_formatted.csv");
        Assert.Equal(FormatterExitCodes.Success, result.ExitCode);
        Assert.Empty(result.StandardError);
        Assert.Contains(outputPath, result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(
            "start_number,time_elapsed,place\r\n"
            + "009,0:09:00,1\r\n"
            + "103,0:48:15,2\r\n"
            + "112,1:02:41.35,3\r\n",
            await File.ReadAllTextAsync(outputPath, Encoding.UTF8));
    }

    [Fact]
    public async Task HeaderOnlyRealCsvFileProducesHeaderOnlyOutput()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        await File.WriteAllTextAsync(
            sourcePath,
            "start_number,time_elapsed\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        ProcessResult result = await RunFormatterProcessAsync(sourcePath);

        Assert.Equal(FormatterExitCodes.Success, result.ExitCode);
        Assert.Equal(
            "start_number,time_elapsed,place\r\n",
            await File.ReadAllTextAsync(Path.Combine(tempDirectory, "competition_formatted.csv"), Encoding.UTF8));
    }

    [Fact]
    public async Task InvalidRealCsvDoesNotReplaceExistingFormattedOutput()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        string outputPath = Path.Combine(tempDirectory, "competition_formatted.csv");
        const string originalOutput = "start_number,time_elapsed,place\r\n001,0:01:00,1\r\n";

        await File.WriteAllTextAsync(
            sourcePath,
            "start_number,time_elapsed\r\n001,not-a-duration\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await File.WriteAllTextAsync(outputPath, originalOutput, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        ProcessResult result = await RunFormatterProcessAsync(sourcePath);

        Assert.Equal(FormatterExitCodes.InvalidInput, result.ExitCode);
        Assert.Contains("not a supported non-negative duration", result.StandardError, StringComparison.Ordinal);
        Assert.Equal(originalOutput, await File.ReadAllTextAsync(outputPath, Encoding.UTF8));
    }

    [Fact]
    public async Task LockedOutputReturnsWriteFailureAndLeavesExistingOutputUnchanged()
    {
        string sourcePath = Path.Combine(tempDirectory, "competition.csv");
        string outputPath = Path.Combine(tempDirectory, "competition_formatted.csv");
        const string originalOutput = "start_number,time_elapsed,place\r\n999,0:09:99,1\r\n";

        await File.WriteAllTextAsync(
            sourcePath,
            "start_number,time_elapsed\r\n001,0:01:00\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await File.WriteAllTextAsync(outputPath, originalOutput, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        ProcessResult result;
        await using (FileStream lockedOutput = new(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = await RunFormatterProcessAsync(sourcePath);
        }

        Assert.Equal(FormatterExitCodes.OutputWriteFailed, result.ExitCode);
        Assert.Contains("Could not write formatted CSV file", result.StandardError, StringComparison.Ordinal);
        Assert.Equal(originalOutput, await File.ReadAllTextAsync(outputPath, Encoding.UTF8));
        Assert.Empty(Directory.GetFiles(tempDirectory, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task MissingAndExtraArgumentsReturnDocumentedInvalidUsageExitCode()
    {
        ProcessResult missing = await RunFormatterProcessAsync();
        ProcessResult extra = await RunFormatterProcessAsync("one.csv", "two.csv");

        Assert.Equal(FormatterExitCodes.InvalidUsage, missing.ExitCode);
        Assert.Equal(FormatterExitCodes.InvalidUsage, extra.ExitCode);
        Assert.Contains("Usage:", missing.StandardError, StringComparison.Ordinal);
        Assert.Contains("Usage:", extra.StandardError, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(tempDirectory, recursive: true);
    }

    private static async Task<ProcessResult> RunFormatterProcessAsync(params string[] args)
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "RaceTimeTracker",
            "RaceTimeTracker.Formatter",
            "RaceTimeTracker.Formatter.csproj");

        List<string> processArgs = ["run", "--no-build", "--project", projectPath, "--"];
        processArgs.AddRange(args);

        using System.Diagnostics.Process process = new()
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        foreach (string argument in processArgs)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RaceTimeTracker", "RaceTimeTracker.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
