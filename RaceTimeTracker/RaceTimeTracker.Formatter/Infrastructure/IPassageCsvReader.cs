using RaceTimeTracker.Formatter.Application;
using RaceTimeTracker.Formatter.Domain;

namespace RaceTimeTracker.Formatter.Infrastructure;

public interface IPassageCsvReader
{
    Task<FormatterResult<IReadOnlyList<PassageRecord>>> ReadAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);
}
