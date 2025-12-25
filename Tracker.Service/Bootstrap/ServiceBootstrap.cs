using Microsoft.Extensions.DependencyInjection;
using Tracker.Data.Infrastructure;
using Tracker.Data.Repositories;

namespace Tracker.Service.Bootstrap
{
    public static class ServiceBootstrap
    {
        public static void Register(IServiceCollection services)
        {
            // Data: database path + connection factory + initializer
            var dbPath = DbPaths.GetDatabaseFilePath();
            services.AddSingleton(new SqliteConnectionFactory(dbPath));
            services.AddSingleton<DbInitializer>();

            // Data: repositories
            services.AddSingleton<GameRepository>();
            services.AddSingleton<SessionRepository>();

            // Hosted worker
            services.AddHostedService<Workers.TrackingWorker>();
        }
    }
}
