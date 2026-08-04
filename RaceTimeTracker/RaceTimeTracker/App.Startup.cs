using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RaceTimeTracker.Application;
using RaceTimeTracker.Infrastructure;

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
        services.AddSingleton<ICompetitionRepository, JsonCompetitionRepository>();
        services.AddSingleton<MainWindow>();
    }
}
