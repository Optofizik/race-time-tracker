using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Application;

/// <summary>
/// UI-independent competition workflow.
/// </summary>
public sealed class CompetitionService : ICompetitionService
{
    private const int MaxNameGenerationAttempts = 100;

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly IClock clock;
    private readonly ICompetitionNameGenerator nameGenerator;
    private readonly ICompetitionRepository repository;
    private readonly IPassageWriter passageWriter;

    public CompetitionService(
        IClock clock,
        ICompetitionNameGenerator nameGenerator,
        ICompetitionRepository repository,
        IPassageWriter passageWriter)
    {
        this.clock = clock;
        this.nameGenerator = nameGenerator;
        this.repository = repository;
        this.passageWriter = passageWriter;
    }

    public async Task<Competition> StartOrResumeAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var competitions = await repository.LoadAsync(cancellationToken).ConfigureAwait(false);
            var activeCompetitions = competitions
                .Where(competition => competition.IsActive)
                .Take(2)
                .ToArray();

            if (activeCompetitions.Length > 1)
            {
                throw new DataStorageException("Multiple active competitions were found.");
            }

            if (activeCompetitions.Length == 1)
            {
                await passageWriter.EnsureCompetitionFileAsync(activeCompetitions[0], cancellationToken)
                    .ConfigureAwait(false);
                return activeCompetitions[0];
            }

            var existingNames = competitions
                .Select(competition => competition.Name)
                .ToHashSet(StringComparer.Ordinal);

            var competition = CreateCompetition(existingNames);
            await repository.AddAsync(competition, cancellationToken).ConfigureAwait(false);
            await passageWriter.EnsureCompetitionFileAsync(competition, cancellationToken).ConfigureAwait(false);

            return competition;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Competition> FinishAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var lookup = await repository.GetActiveCompetitionAsync(cancellationToken).ConfigureAwait(false);

            if (lookup.Status is ActiveCompetitionLookupStatus.Multiple)
            {
                throw new DataStorageException("Multiple active competitions were found.");
            }

            if (lookup.Competition is null)
            {
                throw new CompetitionWorkflowException("There is no active competition to finish.");
            }

            var finishTime = clock.Now;
            await repository.FinishAsync(lookup.Competition.Name, finishTime, cancellationToken)
                .ConfigureAwait(false);

            return new Competition(lookup.Competition.Name, lookup.Competition.StartTime, finishTime);
        }
        finally
        {
            gate.Release();
        }
    }

    private Competition CreateCompetition(IReadOnlySet<string> existingNames)
    {
        for (var attempt = 0; attempt < MaxNameGenerationAttempts; attempt++)
        {
            var candidate = nameGenerator.GenerateCandidate();
            if (!existingNames.Contains(candidate))
            {
                return new Competition(candidate, clock.Now);
            }
        }

        throw new CompetitionWorkflowException("Could not generate a unique competition name.");
    }
}
