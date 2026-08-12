using RaceTimeTracker.Formatter.Application;
using RaceTimeTracker.Formatter.Domain;
using Xunit;

namespace RaceTimeTracker.Formatter.Tests;

public sealed class ProtocolFormatterRuleTests
{
    private readonly ProtocolFormatter formatter = new();

    [Fact]
    public void RunnerIdentityPreservesLeadingZeroesAndUsesOrdinalText()
    {
        PassageRecord[] passages =
        [
            new("007", TimeSpan.FromMinutes(2), 0),
            new("7", TimeSpan.FromMinutes(1), 1),
            new("abc", TimeSpan.FromMinutes(4), 2),
            new("ABC", TimeSpan.FromMinutes(3), 3),
        ];

        IReadOnlyList<ProtocolRecord> protocol = formatter.Format(passages);

        Assert.Equal(["7", "007", "ABC", "abc"], protocol.Select(record => record.StartNumber));
    }

    [Fact]
    public void LatestRecordMeansLastSourceOrderNotGreatestElapsedTime()
    {
        PassageRecord[] passages =
        [
            new("112", TimeSpan.FromHours(2), 0),
            new("103", TimeSpan.FromMinutes(30), 1),
            new("112", TimeSpan.FromMinutes(10), 2),
        ];

        IReadOnlyList<ProtocolRecord> protocol = formatter.Format(passages);

        ProtocolRecord runner112 = Assert.Single(protocol, record => record.StartNumber == "112");
        Assert.Equal(TimeSpan.FromMinutes(10), runner112.Elapsed);
    }

    [Fact]
    public void LatestRecordUsesNormalizedRunnerIdentity()
    {
        PassageRecord[] passages =
        [
            new(" 112 ", TimeSpan.FromMinutes(20), 0),
            new("112", TimeSpan.FromMinutes(10), 1),
        ];

        IReadOnlyList<ProtocolRecord> protocol = formatter.Format(passages);

        ProtocolRecord runner112 = Assert.Single(protocol);
        Assert.Equal("112", runner112.StartNumber);
        Assert.Equal(TimeSpan.FromMinutes(10), runner112.Elapsed);
    }

    [Fact]
    public void FinishersAreSortedByParsedDurationThenOrdinalStartNumber()
    {
        PassageRecord[] passages =
        [
            new("200", TimeSpan.FromMinutes(10), 0),
            new("100", TimeSpan.FromMinutes(2), 1),
            new("050", TimeSpan.FromMinutes(2), 2),
        ];

        IReadOnlyList<ProtocolRecord> protocol = formatter.Format(passages);

        Assert.Equal(["050", "100", "200"], protocol.Select(record => record.StartNumber));
    }

    [Fact]
    public void EqualElapsedTimesReceiveDistinctSequentialPlaces()
    {
        PassageRecord[] passages =
        [
            new("B", TimeSpan.FromMinutes(1), 0),
            new("A", TimeSpan.FromMinutes(1), 1),
        ];

        IReadOnlyList<ProtocolRecord> protocol = formatter.Format(passages);

        Assert.Collection(
            protocol,
            first =>
            {
                Assert.Equal("A", first.StartNumber);
                Assert.Equal(1, first.Place);
            },
            second =>
            {
                Assert.Equal("B", second.StartNumber);
                Assert.Equal(2, second.Place);
            });
    }

    [Fact]
    public void PlacesAreSequentialAfterSortingAllFinishers()
    {
        PassageRecord[] passages =
        [
            new("300", TimeSpan.FromMinutes(3), 0),
            new("100", TimeSpan.FromMinutes(1), 1),
            new("200", TimeSpan.FromMinutes(2), 2),
        ];

        IReadOnlyList<ProtocolRecord> protocol = formatter.Format(passages);

        Assert.Equal([1, 2, 3], protocol.Select(record => record.Place));
        Assert.Equal(["100", "200", "300"], protocol.Select(record => record.StartNumber));
    }

    [Fact]
    public void EmptyInputCreatesEmptyProtocol()
    {
        IReadOnlyList<ProtocolRecord> protocol = formatter.Format([]);

        Assert.Empty(protocol);
    }
}
