using RaceTimeTracker.Formatter.Application;

namespace RaceTimeTracker.Formatter;

public sealed class FormatterConsoleRunner
{
    private readonly FormatterApplication application;
    private readonly TextWriter standardOutput;
    private readonly TextWriter standardError;

    public FormatterConsoleRunner(
        FormatterApplication application,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        this.application = application;
        this.standardOutput = standardOutput;
        this.standardError = standardError;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            FormatterResult<string> argumentResult = ValidateAndResolveSourcePath(args);
            if (!argumentResult.IsSuccess)
            {
                await WriteErrorAsync(argumentResult.Error.Message).ConfigureAwait(false);
                return MapExitCode(argumentResult.Error.Kind);
            }

            FormatterResult<FormatterApplicationSuccess> result =
                await application.FormatAsync(argumentResult.Value, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                await WriteErrorAsync(result.Error.Message).ConfigureAwait(false);
                return MapExitCode(result.Error.Kind);
            }

            await standardOutput.WriteLineAsync(
                $"Created '{result.Value.OutputPath}' with {result.Value.RankedRunnerCount} ranked runner(s).").ConfigureAwait(false);

            return FormatterExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            await WriteErrorAsync("Unexpected formatter failure.").ConfigureAwait(false);
            return FormatterExitCodes.UnexpectedFailure;
        }
    }

    private static FormatterResult<string> ValidateAndResolveSourcePath(string[] args)
    {
        if (args.Length != 1)
        {
            return FormatterResult<string>.Failure(
                new FormatterError(
                    FormatterErrorKind.InvalidUsage,
                    "Usage: RaceTimeTracker.Formatter <csv-file>"));
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(args[0]);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or IOException
            or UnauthorizedAccessException)
        {
            return FormatterResult<string>.Failure(
                new FormatterError(
                    FormatterErrorKind.InvalidUsage,
                    $"Invalid source path '{args[0]}'.",
                    args[0]));
        }

        if (!File.Exists(fullPath))
        {
            string message = Directory.Exists(fullPath)
                ? $"Source path '{fullPath}' is a directory, not a CSV file."
                : $"Source CSV file '{fullPath}' was not found or is inaccessible.";

            return FormatterResult<string>.Failure(
                new FormatterError(FormatterErrorKind.SourceUnavailable, message, fullPath));
        }

        if (!Path.GetExtension(fullPath).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return FormatterResult<string>.Failure(
                new FormatterError(
                    FormatterErrorKind.InvalidUsage,
                    $"Source file '{fullPath}' must have a .csv extension.",
                    fullPath));
        }

        return FormatterResult<string>.Success(fullPath);
    }

    private async Task WriteErrorAsync(string message)
    {
        await standardError.WriteLineAsync(message).ConfigureAwait(false);
    }

    private static int MapExitCode(FormatterErrorKind kind)
    {
        return kind switch
        {
            FormatterErrorKind.InvalidUsage => FormatterExitCodes.InvalidUsage,
            FormatterErrorKind.SourceUnavailable => FormatterExitCodes.SourceUnavailable,
            FormatterErrorKind.InvalidInput => FormatterExitCodes.InvalidInput,
            FormatterErrorKind.OutputWriteFailed => FormatterExitCodes.OutputWriteFailed,
            FormatterErrorKind.UnexpectedFailure => FormatterExitCodes.UnexpectedFailure,
            _ => FormatterExitCodes.UnexpectedFailure,
        };
    }
}
