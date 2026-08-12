using RaceTimeTracker.Formatter.Domain;
using Xunit;

namespace RaceTimeTracker.Formatter.Tests;

public sealed class DomainModelTests
{
    [Fact]
    public void PassageRecordRejectsEmptyStartNumber()
    {
        Assert.Throws<ArgumentException>(() => new PassageRecord(" ", TimeSpan.Zero, 0));
    }

    [Fact]
    public void PassageRecordNormalizesSurroundingStartNumberWhitespace()
    {
        PassageRecord passage = new("  007  ", TimeSpan.Zero, 0);

        Assert.Equal("007", passage.StartNumber);
    }

    [Fact]
    public void PassageRecordRejectsNegativeElapsedTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PassageRecord("112", TimeSpan.FromTicks(-1), 0));
    }

    [Fact]
    public void PassageRecordRejectsNegativeSourceOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PassageRecord("112", TimeSpan.Zero, -1));
    }

    [Fact]
    public void ProtocolRecordRejectsNonPositivePlace()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProtocolRecord("112", TimeSpan.Zero, 0));
    }

    [Fact]
    public void ProtocolRecordNormalizesSurroundingStartNumberWhitespace()
    {
        ProtocolRecord protocol = new("  007  ", TimeSpan.Zero, 1);

        Assert.Equal("007", protocol.StartNumber);
    }
}
