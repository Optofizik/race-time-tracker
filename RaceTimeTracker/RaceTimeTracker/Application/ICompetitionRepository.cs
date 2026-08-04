using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Application;

/// <summary>
/// Stores and retrieves competition metadata from the local data store.
/// </summary>
public interface ICompetitionRepository
{
    Task<IReadOnlyList<Competition>> LoadAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Competition competition, CancellationToken cancellationToken = default);

    Task FinishAsync(string competitionName, DateTime finishTime, CancellationToken cancellationToken = default);
}
