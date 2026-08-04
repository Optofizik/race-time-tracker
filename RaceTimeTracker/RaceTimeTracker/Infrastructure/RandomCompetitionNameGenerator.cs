using RaceTimeTracker.Application;

namespace RaceTimeTracker.Infrastructure;

public sealed class RandomCompetitionNameGenerator : ICompetitionNameGenerator
{
    public string GenerateCandidate() => $"competition_{Random.Shared.Next(1000, 10000)}";
}
