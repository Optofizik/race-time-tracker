using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Application;

/// <summary>
/// Result of checking persistence for unfinished competitions.
/// </summary>
public sealed record ActiveCompetitionLookup
{
    private ActiveCompetitionLookup(ActiveCompetitionLookupStatus status, Competition? competition)
    {
        Status = status;
        Competition = competition;
    }

    public ActiveCompetitionLookupStatus Status { get; }

    public Competition? Competition { get; }

    public static ActiveCompetitionLookup None() => new(ActiveCompetitionLookupStatus.None, null);

    public static ActiveCompetitionLookup Single(Competition competition) =>
        new(ActiveCompetitionLookupStatus.Single, competition);

    public static ActiveCompetitionLookup Multiple() => new(ActiveCompetitionLookupStatus.Multiple, null);
}
