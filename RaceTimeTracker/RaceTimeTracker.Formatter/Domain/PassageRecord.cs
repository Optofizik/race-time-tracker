namespace RaceTimeTracker.Formatter.Domain;

public sealed record PassageRecord
{
    public PassageRecord(string startNumber, TimeSpan elapsed, int sourceOrder)
    {
        string normalizedStartNumber = startNumber.Trim();

        if (normalizedStartNumber.Length == 0)
        {
            throw new ArgumentException("Start number cannot be empty.", nameof(startNumber));
        }

        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "Elapsed time cannot be negative.");
        }

        if (sourceOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOrder), sourceOrder, "Source order cannot be negative.");
        }

        StartNumber = normalizedStartNumber;
        Elapsed = elapsed;
        SourceOrder = sourceOrder;
    }

    public string StartNumber { get; }

    public TimeSpan Elapsed { get; }

    public int SourceOrder { get; }
}
