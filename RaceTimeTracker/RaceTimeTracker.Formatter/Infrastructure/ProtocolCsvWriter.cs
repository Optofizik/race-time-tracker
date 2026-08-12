using System.Globalization;
using System.Text;
using RaceTimeTracker.Formatter.Application;
using RaceTimeTracker.Formatter.Domain;

namespace RaceTimeTracker.Formatter.Infrastructure;

public sealed class ProtocolCsvWriter : IProtocolCsvWriter
{
    private const string Header = "start_number,time_elapsed,place";

    public async Task<FormatterResult<string>> WriteAsync(
        string sourcePath,
        IReadOnlyList<ProtocolRecord> protocolRecords,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protocolRecords);

        string? tempPath = null;

        try
        {
            string outputPath = DeriveOutputPath(sourcePath);
            if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
            {
                return Failure(outputPath, "Computed output path must not equal the source path.");
            }

            string? destinationDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(destinationDirectory))
            {
                destinationDirectory = Directory.GetCurrentDirectory();
            }

            tempPath = Path.Combine(
                destinationDirectory,
                $".{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp");

            await using (FileStream stream = new(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            await using (StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.NewLine = "\r\n";
                await writer.WriteLineAsync(Header.AsMemory(), cancellationToken).ConfigureAwait(false);

                foreach (ProtocolRecord record in protocolRecords)
                {
                    string line = string.Create(
                        CultureInfo.InvariantCulture,
                        $"{EscapeCsv(record.StartNumber)},{FormatDuration(record.Elapsed)},{record.Place}");

                    await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
            }

            File.Move(tempPath, outputPath, overwrite: true);
            tempPath = null;

            return FormatterResult<string>.Success(outputPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            CleanupTempFile(tempPath);

            string path = SafeDeriveOutputPath(sourcePath) ?? sourcePath;
            return Failure(path, $"Could not write formatted CSV file '{path}'.");
        }
    }

    public static string DeriveOutputPath(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        string? directory = Path.GetDirectoryName(sourcePath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourcePath);
        string outputFileName = $"{fileNameWithoutExtension}_formatted.csv";

        return string.IsNullOrEmpty(directory)
            ? outputFileName
            : Path.Combine(directory, outputFileName);
    }

    public static string FormatDuration(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "Elapsed time cannot be negative.");
        }

        long totalHours = elapsed.Ticks / TimeSpan.TicksPerHour;
        int minutes = elapsed.Minutes;
        int seconds = elapsed.Seconds;
        long fractionalTicks = elapsed.Ticks % TimeSpan.TicksPerSecond;

        string formatted = string.Create(
            CultureInfo.InvariantCulture,
            $"{totalHours}:{minutes:00}:{seconds:00}");

        if (fractionalTicks == 0)
        {
            return formatted;
        }

        string fraction = fractionalTicks.ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
        return string.Create(CultureInfo.InvariantCulture, $"{formatted}.{fraction}");
    }

    public static string EscapeCsv(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        bool requiresQuotes = value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal);

        if (!requiresQuotes)
        {
            return value;
        }

        return string.Create(CultureInfo.InvariantCulture, $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"");
    }

    private static FormatterResult<string> Failure(string path, string message)
    {
        return FormatterResult<string>.Failure(
            new FormatterError(FormatterErrorKind.OutputWriteFailed, message, path));
    }

    private static string? SafeDeriveOutputPath(string sourcePath)
    {
        try
        {
            return DeriveOutputPath(sourcePath);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return null;
        }
    }

    private static void CleanupTempFile(string? tempPath)
    {
        if (string.IsNullOrWhiteSpace(tempPath) || !File.Exists(tempPath))
        {
            return;
        }

        try
        {
            File.Delete(tempPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup only. The caller receives the original write failure.
        }
    }
}
