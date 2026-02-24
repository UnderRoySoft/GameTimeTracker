using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tracker.Core.Tracking;
using Tracker.Data.Infrastructure;
using Tracker.Data.Repositories;

namespace Tracker.Tray.Services
{
    public sealed class TrayTrackingHost : IDisposable
    {
        private readonly ILogger<TrayTrackingHost> _logger;
        private readonly DbInitializer _dbInitializer;
        private readonly GameRepository _games;
        private readonly RuleRepository _rules;
        private readonly TrackingCoordinator _coordinator;

        private CancellationTokenSource? _cts;
        private Task? _runTask;

        public TrayTrackingHost(
            ILogger<TrayTrackingHost> logger,
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

        public async Task StartAsync()
        {
            _logger.LogInformation("TrayTrackingHost starting…");

            await _dbInitializer.InitializeAsync();
            _logger.LogInformation("DB initialized at {DbPath}", DbPaths.GetDatabaseFilePath());

            // Optional: seed if no rules exist, so it "does something" on first run.
            if (await _rules.CountRulesAsync() == 0)
            {
                var gameId = await _games.CreateOrGetAsync("My Game (replace rule)");
                await _rules.AddRuleAsync(gameId, "notepad.exe", priority: 1000);
                _logger.LogWarning("Seeded rule: notepad.exe -> My Game (replace rule). Change it in Tracker.UI.");
            }

            _cts = new CancellationTokenSource();
            var poll = TimeSpan.FromSeconds(1);
            var idleThreshold = TimeSpan.FromSeconds(120);

            _runTask = _coordinator.RunAsync(poll, idleThreshold, _cts.Token);
        }

        public async Task StopAsync()
        {
            _logger.LogInformation("TrayTrackingHost stopping…");

            if (_cts is null) return;

            _cts.Cancel();

            if (_runTask is not null)
            {
                try { await _runTask; } catch { /* ignore cancellation */ }
            }
        }

        public void Dispose()
        {
            _cts?.Dispose();
        }
    }
}
