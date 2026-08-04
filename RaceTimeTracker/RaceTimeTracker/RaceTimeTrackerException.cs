namespace RaceTimeTracker;

/// <summary>
/// Base exception for expected application failures that can be presented to an operator.
/// </summary>
public abstract class RaceTimeTrackerException : Exception
{
    protected RaceTimeTrackerException(string message)
        : base(message)
    {
    }

    protected RaceTimeTrackerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
