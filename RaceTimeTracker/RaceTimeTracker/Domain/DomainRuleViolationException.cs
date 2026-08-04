namespace RaceTimeTracker.Domain;

/// <summary>
/// Indicates that an attempted operation would violate a domain invariant.
/// </summary>
public sealed class DomainRuleViolationException : RaceTimeTrackerException
{
    public DomainRuleViolationException(string message)
        : base(message)
    {
    }
}
