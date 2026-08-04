using System.Windows;
using System.Windows.Input;
using RaceTimeTracker.Presentation;

namespace RaceTimeTracker;

public partial class MainWindow
{
    private readonly MainWindowViewModel? viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        this.viewModel = viewModel;

        InitializeComponent();
        DataContext = viewModel;

        Loaded += HandleLoaded;
        Activated += HandleActivated;
        Closed += HandleClosed;
        viewModel.InputFocusRequested += HandleStartNumberFocusRequested;
    }

    private void HandleLoaded(object sender, RoutedEventArgs e) => RestoreStartNumberFocus();

    private void HandleActivated(object? sender, EventArgs e) => RestoreStartNumberFocus();

    private void HandleStartNumberFocusRequested(object? sender, EventArgs e) => RestoreStartNumberFocus();

    private void HandleClosed(object? sender, EventArgs e)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.InputFocusRequested -= HandleStartNumberFocusRequested;
        viewModel.Dispose();
    }

    private void RestoreStartNumberFocus()
    {
        Dispatcher.BeginInvoke(() =>
        {
            StartNumberTextBox.Focus();
            Keyboard.Focus(StartNumberTextBox);
        });
    }
}
