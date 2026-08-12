namespace RaceTimeTracker.Formatter.Application;

public sealed class FormatterResult<T>
{
    private readonly T? value;
    private readonly FormatterError? error;

    private FormatterResult(T value)
    {
        this.value = value;
    }

    private FormatterResult(FormatterError error)
    {
        this.error = error;
    }

    public bool IsSuccess => error is null;

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed formatter result does not contain a value.");

    public FormatterError Error => error
        ?? throw new InvalidOperationException("A successful formatter result does not contain an error.");

    public static FormatterResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new FormatterResult<T>(value);
    }

    public static FormatterResult<T> Failure(FormatterError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new FormatterResult<T>(error);
    }
}
