using Microsoft.Extensions.DependencyInjection;
using Tracker.Core.Abstractions;
using Tracker.Core.Monitoring;
using Tracker.Core.Services;
using Tracker.Core.Tracking;
using Tracker.Data.Infrastructure;
using Tracker.Data.Repositories;

namespace Tracker.Service.Bootstrap
{
    public static class ServiceBootstrap
    {
        public static void Register(IServiceCollection services)
        {
            // Core monitoring
            services.AddSingleton<IForegroundAppDetector, ForegroundAppDetector>();

            // Core services
            services.AddSingleton<InputIdleDetector>();
            services.AddSingleton<TrackingCoordinator>();

            // Data
            var dbPath = DbPaths.GetDatabaseFilePath();
            services.AddSingleton(new SqliteConnectionFactory(dbPath));
            services.AddSingleton<DbInitializer>();

            services.AddSingleton<GameRepository>();
            services.AddSingleton<SessionRepository>();
            services.AddSingleton<RuleRepository>();

            // Hosted worker
            services.AddHostedService<Workers.TrackingWorker>();
        }
    }
}
