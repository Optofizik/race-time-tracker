using RaceTimeTracker.Formatter.Domain;

namespace RaceTimeTracker.Formatter.Application;

public sealed class ProtocolFormatter
{
    public IReadOnlyList<ProtocolRecord> Format(IEnumerable<PassageRecord> passageRecords)
    {
        ArgumentNullException.ThrowIfNull(passageRecords);

        Dictionary<string, PassageRecord> latestRecords = new(StringComparer.Ordinal);

        foreach (PassageRecord passageRecord in passageRecords)
        {
            if (!latestRecords.TryGetValue(passageRecord.StartNumber, out PassageRecord? current)
                || passageRecord.SourceOrder > current.SourceOrder)
            {
                latestRecords[passageRecord.StartNumber] = passageRecord;
            }
        }

        return latestRecords.Values
            .OrderBy(record => record.Elapsed)
            .ThenBy(record => record.StartNumber, StringComparer.Ordinal)
            .Select((record, index) => new ProtocolRecord(record.StartNumber, record.Elapsed, index + 1))
            .ToArray();
    }
}
