using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tracker.Data.Infrastructure;
using Tracker.Data.Repositories;
using Tracker.UI.ViewModels;

namespace Tracker.UI
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // Data: same DB path as service
                    var dbPath = DbPaths.GetDatabaseFilePath();
                    services.AddSingleton(new SqliteConnectionFactory(dbPath));
                    services.AddSingleton<DbInitializer>();

                    // Repos
                    services.AddSingleton<GameRepository>();
                    services.AddSingleton<SessionRepository>();
                    services.AddSingleton<RuleRepository>();

                    // ViewModels
                    services.AddSingleton<DashboardViewModel>();
                    services.AddSingleton<RulesViewModel>();
                    services.AddSingleton<MainViewModel>();

                    // Window
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            // Ensure schema exists for UI usage too
            var dbInit = _host.Services.GetRequiredService<DbInitializer>();
            dbInit.InitializeAsync().GetAwaiter().GetResult();

            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;      // <-- add this line
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
