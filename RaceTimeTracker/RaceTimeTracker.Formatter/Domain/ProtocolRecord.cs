namespace RaceTimeTracker.Formatter.Domain;

public sealed record ProtocolRecord
{
    public ProtocolRecord(string startNumber, TimeSpan elapsed, int place)
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

        if (place <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(place), place, "Place must be a positive integer.");
        }

        StartNumber = normalizedStartNumber;
        Elapsed = elapsed;
        Place = place;
    }

    public string StartNumber { get; }

    public TimeSpan Elapsed { get; }

    public int Place { get; }
}
