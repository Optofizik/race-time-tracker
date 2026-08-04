using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RaceTimeTracker.Application;
using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Infrastructure;

/// <summary>
/// Stores runner passages in one append-only CSV file per competition.
/// </summary>
public sealed partial class CsvPassageWriter : IPassageWriter
{
    private const string Header = "start_number,time_elapsed";
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string storageDirectory;

    public CsvPassageWriter()
        : this(AppContext.BaseDirectory)
    {
    }

    public CsvPassageWriter(string storageDirectory)
    {
        if (string.IsNullOrWhiteSpace(storageDirectory))
        {
            throw new ArgumentException("A storage directory is required.", nameof(storageDirectory));
        }

        this.storageDirectory = storageDirectory;
    }

    public async Task EnsureCompetitionFileAsync(
        Competition competition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(competition);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await EnsureCompetitionFileCoreAsync(competition, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task AppendAsync(
        Competition competition,
        Passage passage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(competition);
        ArgumentNullException.ThrowIfNull(passage);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await EnsureCompetitionFileCoreAsync(competition, cancellationToken).ConfigureAwait(false);

            var filePath = GetFilePath(competition);
            var line = string.Join(
                ',',
                EscapeCsvField(passage.StartNumber),
                EscapeCsvField(passage.ElapsedTime.ToString("c", CultureInfo.InvariantCulture)));

            await using var stream = new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using var writer = new StreamWriter(stream, Utf8NoBom);

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DataStorageException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new DataStorageException(
                $"Could not append passage to '{competition.Name}.csv'.",
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DataStorageException(
                $"Access to '{competition.Name}.csv' was denied.",
                ex);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureCompetitionFileCoreAsync(
        Competition competition,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(storageDirectory);

            var filePath = GetFilePath(competition);
            if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
            {
                return;
            }

            var temporaryFilePath = Path.Combine(
                storageDirectory,
                $"{competition.Name}.csv.{Guid.NewGuid():N}.tmp");

            try
            {
                await using (var stream = new FileStream(
                    temporaryFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                await using (var writer = new StreamWriter(stream, Utf8NoBom))
                {
                    await writer.WriteLineAsync(Header.AsMemory(), cancellationToken).ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                if (File.Exists(filePath))
                {
                    File.Replace(temporaryFilePath, filePath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temporaryFilePath, filePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryFilePath))
                {
                    File.Delete(temporaryFilePath);
                }
            }
        }
        catch (DataStorageException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new DataStorageException(
                $"Could not create '{competition.Name}.csv'.",
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DataStorageException(
                $"Access to '{competition.Name}.csv' was denied.",
                ex);
        }
    }

    private string GetFilePath(Competition competition)
    {
        if (!CompetitionNameRegex().IsMatch(competition.Name))
        {
            throw new DataStorageException(
                $"Competition name '{competition.Name}' must match competition_<digits>.");
        }

        return Path.Combine(storageDirectory, $"{competition.Name}.csv");
    }

    private static string EscapeCsvField(string value)
    {
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\r') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    [GeneratedRegex("^competition_[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CompetitionNameRegex();
}
