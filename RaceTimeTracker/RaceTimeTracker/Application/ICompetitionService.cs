using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Application;

/// <summary>
/// Coordinates competition start, resume, and finish operations.
/// </summary>
public interface ICompetitionService
{
    Task<Competition> StartOrResumeAsync(CancellationToken cancellationToken = default);

    Task<Competition> FinishAsync(CancellationToken cancellationToken = default);
}
