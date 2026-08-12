namespace RaceTimeTracker.Formatter.Application;

public sealed record FormatterError(
    FormatterErrorKind Kind,
    string Message,
    string? Path = null,
    int? RecordNumber = null);
