namespace RaceTimeTracker.Application;

/// <summary>
/// Indicates that the requested workflow action is not valid in the current competition state.
/// </summary>
public sealed class CompetitionWorkflowException : RaceTimeTrackerException
{
    public CompetitionWorkflowException(string message)
        : base(message)
    {
    }

    public CompetitionWorkflowException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
