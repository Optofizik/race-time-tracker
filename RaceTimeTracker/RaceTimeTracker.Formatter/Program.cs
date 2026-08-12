using RaceTimeTracker.Formatter;
using RaceTimeTracker.Formatter.Application;
using RaceTimeTracker.Formatter.Infrastructure;

FormatterApplication application = new(
    new PassageCsvReader(),
    new ProtocolFormatter(),
    new ProtocolCsvWriter());

FormatterConsoleRunner runner = new(application, Console.Out, Console.Error);

return await runner.RunAsync(args).ConfigureAwait(false);
