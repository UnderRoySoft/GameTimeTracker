using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tracker.Data.Infrastructure;
using Tracker.Data.Repositories;

namespace Tracker.Service.Workers
{
    public sealed class TrackingWorker : BackgroundService
    {
        private readonly ILogger<TrackingWorker> _logger;
        private readonly DbInitializer _dbInitializer;
        private readonly GameRepository _games;
        private readonly SessionRepository _sessions;

        public TrackingWorker(
            ILogger<TrackingWorker> logger,
            DbInitializer dbInitializer,
            GameRepository games,
            SessionRepository sessions)
        {
            _logger = logger;
            _dbInitializer = dbInitializer;
            _games = games;
            _sessions = sessions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TrackingWorker started at {Time}", DateTimeOffset.Now);

            // 1) Initialize database + schema
            await _dbInitializer.InitializeAsync();
            _logger.LogInformation("Database initialized at {DbPath}", DbPaths.GetDatabaseFilePath());

            // 2) Smoke test: create a sample game and one short session
            var gameId = await _games.CreateOrGetAsync("SmokeTest Game");
            var sessionId = await _sessions.StartSessionAsync(gameId, source: "process");

            _logger.LogInformation("Smoke session started. GameId={GameId}, SessionId={SessionId}", gameId, sessionId);

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            await _sessions.StopSessionAsync(sessionId, idleSeconds: 0);

            _logger.LogInformation("Smoke session stopped. SessionId={SessionId}", sessionId);

            // 3) Keep service alive (heartbeat)
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Heartbeat at {Time}", DateTimeOffset.Now);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
