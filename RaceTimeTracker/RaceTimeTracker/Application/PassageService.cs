using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Application;

/// <summary>
/// UI-independent runner passage workflow.
/// </summary>
public sealed class PassageService : IPassageService
{
    private readonly IClock clock;
    private readonly ICompetitionRepository repository;
    private readonly IPassageWriter passageWriter;

    public PassageService(
        IClock clock,
        ICompetitionRepository repository,
        IPassageWriter passageWriter)
    {
        this.clock = clock;
        this.repository = repository;
        this.passageWriter = passageWriter;
    }

    public async Task<Passage> RecordAsync(
        string startNumber,
        CancellationToken cancellationToken = default)
    {
        var lookup = await repository.GetActiveCompetitionAsync(cancellationToken).ConfigureAwait(false);

        if (lookup.Status is ActiveCompetitionLookupStatus.Multiple)
        {
            throw new DataStorageException("Multiple active competitions were found.");
        }

        if (lookup.Competition is null)
        {
            throw new CompetitionWorkflowException("Start a competition before recording passages.");
        }

        var elapsedTime = lookup.Competition.GetElapsedTime(clock.Now);
        var passage = new Passage(startNumber.Trim(), elapsedTime);

        await passageWriter.AppendAsync(lookup.Competition, passage, cancellationToken)
            .ConfigureAwait(false);

        return passage;
    }
}
