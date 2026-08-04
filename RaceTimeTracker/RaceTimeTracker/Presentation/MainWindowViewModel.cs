using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using RaceTimeTracker.Application;
using RaceTimeTracker.Domain;

namespace RaceTimeTracker.Presentation;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IClock clock;
    private readonly ICompetitionService competitionService;
    private readonly IPassageService passageService;
    private readonly DispatcherTimer timer;
    private Competition? activeCompetition;
    private bool isBusy;
    private string startNumber = string.Empty;
    private string statusMessage = "Ready.";

    public MainWindowViewModel(
        IClock clock,
        ICompetitionService competitionService,
        IPassageService passageService)
    {
        this.clock = clock;
        this.competitionService = competitionService;
        this.passageService = passageService;

        StartFinishCommand = new AsyncRelayCommand(ExecuteStartFinishAsync, () => !IsBusy);
        RecordPassageCommand = new AsyncRelayCommand(
            ExecuteRecordPassageAsync,
            () => !IsBusy && IsCompetitionActive && !string.IsNullOrWhiteSpace(StartNumber));

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += HandleTimerTick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? InputFocusRequested;

    public AsyncRelayCommand StartFinishCommand { get; }

    public AsyncRelayCommand RecordPassageCommand { get; }

    public string StartFinishButtonText => IsCompetitionActive ? "Finish" : "Start";

    public string ElapsedTimeText => activeCompetition is null
        ? "00:00:00.00"
        : FormatElapsed(activeCompetition.GetElapsedTime(activeCompetition.FinishTime ?? clock.Now));

    public string CompetitionNameText => activeCompetition?.Name ?? "No active competition";

    public bool IsCompetitionActive => activeCompetition?.IsActive == true;

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                RaiseCommandStatesChanged();
            }
        }
    }

    public string StartNumber
    {
        get => startNumber;
        set
        {
            if (SetField(ref startNumber, value))
            {
                RecordPassageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTimerTick;
    }

    private async Task ExecuteStartFinishAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            if (!IsCompetitionActive)
            {
                activeCompetition = await competitionService.StartOrResumeAsync(cancellationToken)
                    .ConfigureAwait(true);
                timer.Start();
                StatusMessage = $"Competition '{activeCompetition.Name}' is running.";
            }
            else
            {
                activeCompetition = await competitionService.FinishAsync(cancellationToken)
                    .ConfigureAwait(true);
                timer.Stop();
                StatusMessage = $"Competition '{activeCompetition.Name}' finished.";
            }

            RaiseCompetitionStateChanged();
        }
        catch (RaceTimeTrackerException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RequestInputFocus();
        }
    }

    private async Task ExecuteRecordPassageAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            var passage = await passageService.RecordAsync(StartNumber, cancellationToken)
                .ConfigureAwait(true);

            StatusMessage = $"Runner {passage.StartNumber}: {FormatElapsed(passage.ElapsedTime)}.";
            StartNumber = string.Empty;
        }
        catch (RaceTimeTrackerException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RequestInputFocus();
        }
    }

    private void HandleTimerTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ElapsedTimeText));
    }

    private void RaiseCompetitionStateChanged()
    {
        OnPropertyChanged(nameof(StartFinishButtonText));
        OnPropertyChanged(nameof(ElapsedTimeText));
        OnPropertyChanged(nameof(CompetitionNameText));
        OnPropertyChanged(nameof(IsCompetitionActive));
        RaiseCommandStatesChanged();
    }

    private void RaiseCommandStatesChanged()
    {
        StartFinishCommand.RaiseCanExecuteChanged();
        RecordPassageCommand.RaiseCanExecuteChanged();
    }

    private void RequestInputFocus() => InputFocusRequested?.Invoke(this, EventArgs.Empty);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var totalHours = (int)elapsed.TotalHours;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 10:00}");
    }
}
