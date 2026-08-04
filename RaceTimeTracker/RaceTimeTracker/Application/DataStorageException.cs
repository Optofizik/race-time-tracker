namespace RaceTimeTracker.Application;

/// <summary>
/// Indicates an expected failure while reading or writing competition data.
/// </summary>
public sealed class DataStorageException : RaceTimeTrackerException
{
    public DataStorageException(string message)
        : base(message)
    {
    }

    public DataStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
