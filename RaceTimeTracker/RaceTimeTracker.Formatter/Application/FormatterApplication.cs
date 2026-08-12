using RaceTimeTracker.Formatter.Domain;
using RaceTimeTracker.Formatter.Infrastructure;

namespace RaceTimeTracker.Formatter.Application;

public sealed class FormatterApplication
{
    private readonly IPassageCsvReader passageCsvReader;
    private readonly ProtocolFormatter protocolFormatter;
    private readonly IProtocolCsvWriter protocolCsvWriter;

    public FormatterApplication(
        IPassageCsvReader passageCsvReader,
        ProtocolFormatter protocolFormatter,
        IProtocolCsvWriter protocolCsvWriter)
    {
        this.passageCsvReader = passageCsvReader;
        this.protocolFormatter = protocolFormatter;
        this.protocolCsvWriter = protocolCsvWriter;
    }

    public async Task<FormatterResult<FormatterApplicationSuccess>> FormatAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        FormatterResult<IReadOnlyList<PassageRecord>> readResult =
            await passageCsvReader.ReadAsync(sourcePath, cancellationToken).ConfigureAwait(false);

        if (!readResult.IsSuccess)
        {
            return FormatterResult<FormatterApplicationSuccess>.Failure(readResult.Error);
        }

        IReadOnlyList<ProtocolRecord> protocolRecords = protocolFormatter.Format(readResult.Value);
        FormatterResult<string> writeResult =
            await protocolCsvWriter.WriteAsync(sourcePath, protocolRecords, cancellationToken).ConfigureAwait(false);

        if (!writeResult.IsSuccess)
        {
            return FormatterResult<FormatterApplicationSuccess>.Failure(writeResult.Error);
        }

        return FormatterResult<FormatterApplicationSuccess>.Success(
            new FormatterApplicationSuccess(writeResult.Value, protocolRecords.Count));
    }
}
