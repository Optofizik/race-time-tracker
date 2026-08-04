using RaceTimeTracker.Application;

namespace RaceTimeTracker.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
