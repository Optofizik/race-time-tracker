using System.Globalization;
using System.Text;
using RaceTimeTracker.Formatter.Application;
using RaceTimeTracker.Formatter.Domain;

namespace RaceTimeTracker.Formatter.Infrastructure;

public sealed class PassageCsvReader : IPassageCsvReader
{
    private const string ExpectedStartNumberHeader = "start_number";
    private const string ExpectedElapsedHeader = "time_elapsed";

    public async Task<FormatterResult<IReadOnlyList<PassageRecord>>> ReadAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string csv = await File.ReadAllTextAsync(sourcePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            return Parse(csv, sourcePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return FormatterResult<IReadOnlyList<PassageRecord>>.Failure(
                new FormatterError(
                    FormatterErrorKind.SourceUnavailable,
                    $"Could not read source CSV file '{sourcePath}'.",
                    sourcePath));
        }
    }

    private static FormatterResult<IReadOnlyList<PassageRecord>> Parse(string csv, string sourcePath)
    {
        CsvParseResult parsedCsv = ParseCsv(csv);
        if (!parsedCsv.IsSuccess)
        {
            return Failure(sourcePath, parsedCsv.ErrorMessage!, parsedCsv.ErrorRecordNumber);
        }

        List<CsvRecord> records = parsedCsv.Records
            .Where(record => !record.IsBlank)
            .ToList();

        if (records.Count == 0)
        {
            return Failure(sourcePath, "CSV file is empty.", 1);
        }

        CsvRecord header = records[0];
        if (header.Fields.Count != 2
            || RemoveUtf8Bom(header.Fields[0]) != ExpectedStartNumberHeader
            || header.Fields[1] != ExpectedElapsedHeader)
        {
            return Failure(sourcePath, "CSV header must be exactly 'start_number,time_elapsed'.", 1);
        }

        List<PassageRecord> passages = [];
        int sourceOrder = 0;

        foreach (CsvRecord record in records.Skip(1))
        {
            if (record.Fields.Count != 2)
            {
                return Failure(
                    sourcePath,
                    $"Record {record.RecordNumber} must contain exactly two fields.",
                    record.RecordNumber);
            }

            string startNumber = record.Fields[0].Trim();
            if (startNumber.Length == 0)
            {
                return Failure(sourcePath, "Start number cannot be empty.", record.RecordNumber);
            }

            string elapsedText = record.Fields[1].Trim();
            if (elapsedText.Length == 0)
            {
                return Failure(sourcePath, "Elapsed time cannot be empty.", record.RecordNumber);
            }

            if (!TryParseDuration(elapsedText, out TimeSpan elapsed))
            {
                return Failure(
                    sourcePath,
                    $"Elapsed time '{elapsedText}' is not a supported non-negative duration.",
                    record.RecordNumber);
            }

            passages.Add(new PassageRecord(startNumber, elapsed, sourceOrder));
            sourceOrder++;
        }

        return FormatterResult<IReadOnlyList<PassageRecord>>.Success(passages);
    }

    private static CsvParseResult ParseCsv(string csv)
    {
        List<CsvRecord> records = [];
        List<string> fields = [];
        StringBuilder field = new();
        bool inQuotes = false;
        bool fieldWasQuoted = false;
        bool afterClosingQuote = false;
        int recordNumber = 1;

        for (int index = 0; index < csv.Length; index++)
        {
            char current = csv[index];

            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < csv.Length && csv[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterClosingQuote = true;
                    }
                }
                else
                {
                    field.Append(current);
                }

                continue;
            }

            if (afterClosingQuote)
            {
                if (char.IsWhiteSpace(current) && current is not '\r' and not '\n')
                {
                    continue;
                }

                if (current == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    fieldWasQuoted = false;
                    afterClosingQuote = false;
                    continue;
                }

                if (current == '\r' || current == '\n')
                {
                    AddRecord(records, fields, field, fieldWasQuoted, recordNumber);
                    fieldWasQuoted = false;
                    afterClosingQuote = false;

                    if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                    {
                        index++;
                    }

                    recordNumber++;
                    continue;
                }

                return CsvParseResult.Failure(
                    $"Unexpected character after closing quote in record {recordNumber}.",
                    recordNumber);
            }

            if (current == '"')
            {
                if (field.Length == 0 || IsOnlyWhiteSpace(field))
                {
                    field.Clear();
                    inQuotes = true;
                    fieldWasQuoted = true;
                    continue;
                }

                return CsvParseResult.Failure(
                    $"Unexpected quote in unquoted field in record {recordNumber}.",
                    recordNumber);
            }

            if (current == ',')
            {
                fields.Add(fieldWasQuoted ? field.ToString() : field.ToString().Trim());
                field.Clear();
                fieldWasQuoted = false;
                continue;
            }

            if (current == '\r' || current == '\n')
            {
                AddRecord(records, fields, field, fieldWasQuoted, recordNumber);
                fieldWasQuoted = false;

                if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                {
                    index++;
                }

                recordNumber++;
                continue;
            }

            field.Append(current);
        }

        if (inQuotes)
        {
            return CsvParseResult.Failure(
                $"Unterminated quoted field in record {recordNumber}.",
                recordNumber);
        }

        if (afterClosingQuote || fields.Count > 0 || field.Length > 0 || fieldWasQuoted)
        {
            AddRecord(records, fields, field, fieldWasQuoted, recordNumber);
        }

        return CsvParseResult.Success(records);
    }

    private static void AddRecord(
        List<CsvRecord> records,
        List<string> fields,
        StringBuilder field,
        bool fieldWasQuoted,
        int recordNumber)
    {
        fields.Add(fieldWasQuoted ? field.ToString() : field.ToString().Trim());
        records.Add(new CsvRecord(recordNumber, fields.ToArray()));
        fields.Clear();
        field.Clear();
    }

    private static bool IsOnlyWhiteSpace(StringBuilder value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (!char.IsWhiteSpace(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseDuration(string value, out TimeSpan elapsed)
    {
        elapsed = default;

        if (value.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        string[] mainParts = value.Split(':');
        if (mainParts.Length is not (2 or 3))
        {
            return false;
        }

        if (!TryParseNonNegativeInteger(mainParts[0], out long first))
        {
            return false;
        }

        long hours;
        long minutes;
        string secondsPart;

        if (mainParts.Length == 2)
        {
            hours = 0;
            minutes = first;
            secondsPart = mainParts[1];
        }
        else
        {
            if (!TryParseNonNegativeInteger(mainParts[1], out minutes) || minutes > 59)
            {
                return false;
            }

            hours = first;
            secondsPart = mainParts[2];
        }

        if (!TryParseSeconds(secondsPart, out int seconds, out long fractionalTicks))
        {
            return false;
        }

        try
        {
            checked
            {
                long ticks = (hours * TimeSpan.TicksPerHour)
                    + (minutes * TimeSpan.TicksPerMinute)
                    + (seconds * TimeSpan.TicksPerSecond)
                    + fractionalTicks;

                elapsed = new TimeSpan(ticks);
                return true;
            }
        }
        catch (OverflowException)
        {
            elapsed = default;
            return false;
        }
    }

    private static bool TryParseSeconds(string value, out int seconds, out long fractionalTicks)
    {
        seconds = 0;
        fractionalTicks = 0;

        string[] secondParts = value.Split('.');
        if (secondParts.Length is not (1 or 2))
        {
            return false;
        }

        if (!int.TryParse(secondParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out seconds)
            || seconds is < 0 or > 59)
        {
            return false;
        }

        if (secondParts.Length == 1)
        {
            return true;
        }

        string fraction = secondParts[1];
        if (fraction.Length == 0 || fraction.Length > 7 || fraction.Any(character => !char.IsAsciiDigit(character)))
        {
            return false;
        }

        string paddedFraction = fraction.PadRight(7, '0');
        fractionalTicks = long.Parse(paddedFraction, NumberStyles.None, CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryParseNonNegativeInteger(string value, out long parsed)
    {
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed >= 0;
    }

    private static FormatterResult<IReadOnlyList<PassageRecord>> Failure(
        string sourcePath,
        string message,
        int? recordNumber)
    {
        return FormatterResult<IReadOnlyList<PassageRecord>>.Failure(
            new FormatterError(FormatterErrorKind.InvalidInput, message, sourcePath, recordNumber));
    }

    private static string RemoveUtf8Bom(string value)
    {
        return value.Length > 0 && value[0] == '\uFEFF'
            ? value[1..]
            : value;
    }

    private sealed record CsvRecord(int RecordNumber, IReadOnlyList<string> Fields)
    {
        public bool IsBlank => Fields.All(string.IsNullOrWhiteSpace);
    }

    private sealed class CsvParseResult
    {
        private CsvParseResult(IReadOnlyList<CsvRecord> records)
        {
            Records = records;
        }

        private CsvParseResult(string errorMessage, int errorRecordNumber)
        {
            ErrorMessage = errorMessage;
            ErrorRecordNumber = errorRecordNumber;
        }

        public bool IsSuccess => ErrorMessage is null;

        public IReadOnlyList<CsvRecord> Records { get; } = [];

        public string? ErrorMessage { get; }

        public int ErrorRecordNumber { get; }

        public static CsvParseResult Success(IReadOnlyList<CsvRecord> records)
        {
            return new CsvParseResult(records);
        }

        public static CsvParseResult Failure(string errorMessage, int errorRecordNumber)
        {
            return new CsvParseResult(errorMessage, errorRecordNumber);
        }
    }
}
