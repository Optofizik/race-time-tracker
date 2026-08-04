using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using RaceTimeTracker.Application;
using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Infrastructure;

/// <summary>
/// Stores competition metadata in competitions.json beside the executable.
/// </summary>
public sealed partial class JsonCompetitionRepository : ICompetitionRepository
{
    public const string FileName = "competitions.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new RoundTripDateTimeJsonConverter() }
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string filePath;

    public JsonCompetitionRepository()
        : this(AppContext.BaseDirectory)
    {
    }

    public JsonCompetitionRepository(string storageDirectory)
    {
        if (string.IsNullOrWhiteSpace(storageDirectory))
        {
            throw new ArgumentException("A storage directory is required.", nameof(storageDirectory));
        }

        filePath = Path.Combine(storageDirectory, FileName);
    }

    public async Task<IReadOnlyList<Competition>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var competitions = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            return competitions.Values
                .Select(item => new Competition(item.Name, item.Metadata.StartTime, item.Metadata.FinishTime))
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ActiveCompetitionLookup> GetActiveCompetitionAsync(CancellationToken cancellationToken = default)
    {
        var activeCompetitions = (await LoadAsync(cancellationToken).ConfigureAwait(false))
            .Where(competition => competition.IsActive)
            .Take(2)
            .ToArray();

        return activeCompetitions.Length switch
        {
            0 => ActiveCompetitionLookup.None(),
            1 => ActiveCompetitionLookup.Single(activeCompetitions[0]),
            _ => ActiveCompetitionLookup.Multiple()
        };
    }

    public async Task AddAsync(Competition competition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(competition);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var competitions = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

            if (competitions.ContainsKey(competition.Name))
            {
                throw new DataStorageException($"Competition '{competition.Name}' already exists.");
            }

            competitions.Add(
                competition.Name,
                new CompetitionFileEntry(
                    competition.Name,
                    new CompetitionMetadata(competition.StartTime, competition.FinishTime)));

            await SaveCoreAsync(competitions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task FinishAsync(
        string competitionName,
        DateTime finishTime,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(competitionName))
        {
            throw new DataStorageException("A competition name is required.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var competitions = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);

            if (!competitions.TryGetValue(competitionName, out var entry))
            {
                throw new DataStorageException($"Competition '{competitionName}' was not found.");
            }

            var competition = new Competition(
                competitionName,
                entry.Metadata.StartTime,
                entry.Metadata.FinishTime);

            try
            {
                competition.Finish(finishTime);
            }
            catch (DomainRuleViolationException ex)
            {
                throw new DataStorageException(ex.Message, ex);
            }

            competitions[competitionName] = new CompetitionFileEntry(
                competitionName,
                new CompetitionMetadata(competition.StartTime, competition.FinishTime));

            await SaveCoreAsync(competitions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Dictionary<string, CompetitionFileEntry>> LoadCoreAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(filePath))
            {
                await SaveCoreAsync(new Dictionary<string, CompetitionFileEntry>(), cancellationToken)
                    .ConfigureAwait(false);
            }

            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            if (stream.Length == 0)
            {
                throw new DataStorageException($"{FileName} is empty.");
            }

            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return ParseCompetitions(document.RootElement);
        }
        catch (DataStorageException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new DataStorageException($"{FileName} contains invalid JSON.", ex);
        }
        catch (IOException ex)
        {
            throw new DataStorageException($"Could not read {FileName}.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DataStorageException($"Access to {FileName} was denied.", ex);
        }
    }

    private async Task SaveCoreAsync(
        IReadOnlyDictionary<string, CompetitionFileEntry> competitions,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryFilePath = Path.Combine(
            directory ?? string.Empty,
            $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var payload = competitions.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Metadata,
                StringComparer.Ordinal);

            await using (var stream = new FileStream(
                temporaryFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
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
        catch (DataStorageException)
        {
            throw;
        }
        catch (IOException ex)
        {
            throw new DataStorageException($"Could not write {FileName}.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DataStorageException($"Access to {FileName} was denied.", ex);
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    private static Dictionary<string, CompetitionFileEntry> ParseCompetitions(JsonElement root)
    {
        if (root.ValueKind is not JsonValueKind.Object)
        {
            throw new DataStorageException($"{FileName} must contain a JSON object.");
        }

        var competitions = new Dictionary<string, CompetitionFileEntry>(StringComparer.Ordinal);

        foreach (var competitionProperty in root.EnumerateObject())
        {
            var competitionName = competitionProperty.Name;
            ValidateCompetitionName(competitionName);

            if (!competitions.TryAdd(
                    competitionName,
                    new CompetitionFileEntry(
                        competitionName,
                        ParseMetadata(competitionName, competitionProperty.Value))))
            {
                throw new DataStorageException(
                    $"{FileName} contains duplicate competition '{competitionName}'.");
            }
        }

        return competitions;
    }

    private static CompetitionMetadata ParseMetadata(string competitionName, JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.Object)
        {
            throw new DataStorageException($"Competition '{competitionName}' must contain an object value.");
        }

        if (!value.TryGetProperty("startTime", out var startTimeElement))
        {
            throw new DataStorageException($"Competition '{competitionName}' is missing startTime.");
        }

        if (!TryReadDateTime(startTimeElement, out var startTime))
        {
            throw new DataStorageException($"Competition '{competitionName}' has invalid startTime.");
        }

        DateTime? finishTime = null;
        if (!value.TryGetProperty("finishTime", out var finishTimeElement))
        {
            throw new DataStorageException($"Competition '{competitionName}' is missing finishTime.");
        }

        if (finishTimeElement.ValueKind is not JsonValueKind.Null)
        {
            if (!TryReadDateTime(finishTimeElement, out var parsedFinishTime))
            {
                throw new DataStorageException($"Competition '{competitionName}' has invalid finishTime.");
            }

            finishTime = parsedFinishTime;
        }

        try
        {
            _ = new Competition(competitionName, startTime, finishTime);
        }
        catch (DomainRuleViolationException ex)
        {
            throw new DataStorageException(
                $"Competition '{competitionName}' violates domain rules: {ex.Message}",
                ex);
        }

        return new CompetitionMetadata(startTime, finishTime);
    }

    private static bool TryReadDateTime(JsonElement element, out DateTime value)
    {
        value = default;

        if (element.ValueKind is not JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString();
        return DateTime.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out value);
    }

    private static void ValidateCompetitionName(string competitionName)
    {
        if (!CompetitionNameRegex().IsMatch(competitionName))
        {
            throw new DataStorageException(
                $"Competition name '{competitionName}' must match competition_<digits>.");
        }
    }

    [GeneratedRegex("^competition_[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CompetitionNameRegex();

    private sealed record CompetitionFileEntry(string Name, CompetitionMetadata Metadata);

    private sealed record CompetitionMetadata(DateTime StartTime, DateTime? FinishTime);

    private sealed class RoundTripDateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType is not JsonTokenType.String)
            {
                throw new JsonException("Expected a string DateTime value.");
            }

            var text = reader.GetString();
            if (!DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var value))
            {
                throw new JsonException("Invalid DateTime value.");
            }

            return value;
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTime value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
        }
    }
}
