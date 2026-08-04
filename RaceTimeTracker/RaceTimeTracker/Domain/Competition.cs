namespace RaceTimeTracker.Domain;

/// <summary>
/// Represents one locally timed competition.
/// </summary>
public sealed class Competition
{
    public Competition(string name, DateTime startTime, DateTime? finishTime = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleViolationException("A competition name is required.");
        }

        if (finishTime is not null && finishTime < startTime)
        {
            throw new DomainRuleViolationException("The finish time cannot be earlier than the start time.");
        }

        Name = name;
        StartTime = startTime;
        FinishTime = finishTime;
    }

    public string Name { get; }

    public DateTime StartTime { get; }

    public DateTime? FinishTime { get; private set; }

    public bool IsActive => FinishTime is null;

    public TimeSpan GetElapsedTime(DateTime timestamp)
    {
        var elapsed = timestamp - StartTime;

        if (elapsed < TimeSpan.Zero)
        {
            throw new DomainRuleViolationException("Elapsed time cannot be negative.");
        }

        return elapsed;
    }

    public void Finish(DateTime finishTime)
    {
        if (!IsActive)
        {
            throw new DomainRuleViolationException("A finished competition cannot be finished again.");
        }

        if (finishTime < StartTime)
        {
            throw new DomainRuleViolationException("The finish time cannot be earlier than the start time.");
        }

        FinishTime = finishTime;
    }
}
