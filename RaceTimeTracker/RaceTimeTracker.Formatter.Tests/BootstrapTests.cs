using RaceTimeTracker.Formatter.Application;
using Xunit;

namespace RaceTimeTracker.Formatter.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void FormatterTestProjectReferencesFormatterAssembly()
    {
        Type formatterType = typeof(ProtocolFormatter);

        Assert.Equal("RaceTimeTracker.Formatter.Application.ProtocolFormatter", formatterType.FullName);
    }
}
