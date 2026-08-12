using RaceTimeTracker.Formatter.Application;
using RaceTimeTracker.Formatter.Domain;

namespace RaceTimeTracker.Formatter.Infrastructure;

public interface IProtocolCsvWriter
{
    Task<FormatterResult<string>> WriteAsync(
        string sourcePath,
        IReadOnlyList<ProtocolRecord> protocolRecords,
        CancellationToken cancellationToken = default);
}
