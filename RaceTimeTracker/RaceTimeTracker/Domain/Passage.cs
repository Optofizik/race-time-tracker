namespace RaceTimeTracker.Domain;

/// <summary>
/// A single runner passage recorded during a competition.
/// </summary>
public sealed record Passage
{
    public Passage(string startNumber, TimeSpan elapsedTime)
    {
        if (string.IsNullOrWhiteSpace(startNumber) || !startNumber.All(char.IsAsciiDigit))
        {
            throw new DomainRuleViolationException("The start number must contain digits only.");
        }

        if (elapsedTime < TimeSpan.Zero)
        {
            throw new DomainRuleViolationException("Passage elapsed time cannot be negative.");
        }

        StartNumber = startNumber;
        ElapsedTime = elapsedTime;
    }

    public string StartNumber { get; }

    public TimeSpan ElapsedTime { get; }
}
