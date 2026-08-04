using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Application;

/// <summary>
/// Manages append-only CSV passage records for a competition.
/// </summary>
public interface IPassageWriter
{
    Task EnsureCompetitionFileAsync(Competition competition, CancellationToken cancellationToken = default);

    Task AppendAsync(Competition competition, Passage passage, CancellationToken cancellationToken = default);
}
