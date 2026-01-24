using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tracker.Core.Tracking;
using Tracker.Data.Infrastructure;
using Tracker.Data.Repositories;

namespace Tracker.Service.Workers
{
    public sealed class TrackingWorker : BackgroundService
    {
        private readonly ILogger<TrackingWorker> _logger;
        private readonly DbInitializer _dbInitializer;
        private readonly GameRepository _games;
        private readonly RuleRepository _rules;
        private readonly TrackingCoordinator _coordinator;

        public TrackingWorker(
            ILogger<TrackingWorker> logger,
            DbInitializer dbInitializer,
            GameRepository games,
            RuleRepository rules,
            TrackingCoordinator coordinator)
        {
            _logger = logger;
            _dbInitializer = dbInitializer;
            _games = games;
            _rules = rules;
            _coordinator = coordinator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Tracker.Service starting at {Time}", DateTimeOffset.Now);

            // 1) Ensure DB exists + schema applied
            await _dbInitializer.InitializeAsync();
            _logger.LogInformation("Database initialized at {DbPath}", DbPaths.GetDatabaseFilePath());

            // 2) Seed at least one rule if DB has no rules yet
            //    This is only to make first run usable. You will replace it later with UI rule management.
            var ruleCount = await _rules.CountRulesAsync();
            if (ruleCount == 0)
            {
                _logger.LogWarning("No executable rules found. Seeding a sample rule.");

                // Create a game called "My Game" and map notepad.exe to it as a demo.
                // Later you will replace notepad.exe with real game exe names.
                var gameId = await _games.CreateOrGetAsync("My Game (replace exe rule)");
                await _rules.AddRuleAsync(gameId, "witcher.exe", priority: 1000);

                _logger.LogWarning("Seeded: notepad.exe -> 'My Game (replace exe rule)'. Change this to your real game exe.");
            }

            // 3) Start tracking loop
            var poll = TimeSpan.FromSeconds(1);
            var idleThreshold = TimeSpan.FromSeconds(120); // 2 minutes

            await _coordinator.RunAsync(poll, idleThreshold, stoppingToken);
        }
    }
}
