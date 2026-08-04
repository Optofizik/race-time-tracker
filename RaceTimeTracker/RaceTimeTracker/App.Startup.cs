using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RaceTimeTracker.Application;
using RaceTimeTracker.Infrastructure;
using RaceTimeTracker.Presentation;

namespace RaceTimeTracker;

public partial class App
{
    private ServiceProvider? serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICompetitionNameGenerator, RandomCompetitionNameGenerator>();
        services.AddSingleton<ICompetitionRepository, JsonCompetitionRepository>();
        services.AddSingleton<IPassageWriter, CsvPassageWriter>();
        services.AddSingleton<ICompetitionService, CompetitionService>();
        services.AddSingleton<IPassageService, PassageService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
