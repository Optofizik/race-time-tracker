using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Application;

/// <summary>
/// Coordinates runner passage validation and recording.
/// </summary>
public interface IPassageService
{
    Task<Passage> RecordAsync(string startNumber, CancellationToken cancellationToken = default);
}
