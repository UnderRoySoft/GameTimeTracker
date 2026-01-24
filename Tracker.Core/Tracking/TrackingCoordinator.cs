using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using Tracker.Core.Abstractions;
using Tracker.Core.Services;

using Tracker.Data.Repositories;


namespace Tracker.Core.Tracking
{
    /// <summary>
    /// Orchestrates:
    /// - Foreground detection
    /// - Rule matching (exe -> game)
    /// - Session start/stop
    /// - Idle accumulation
    /// </summary>
    public sealed class TrackingCoordinator
    {
        private readonly IForegroundAppDetector _foreground;
        private readonly InputIdleDetector _idle;
        private readonly RuleRepository _rules;
        private readonly SessionRepository _sessions;
        private readonly ILogger<TrackingCoordinator> _logger;

        private long? _activeGameId;
        private long? _activeSessionId;
        private int _idleSecondsAccumulated;

        public TrackingCoordinator(
            IForegroundAppDetector foreground,
            InputIdleDetector idle,
            RuleRepository rules,
            SessionRepository sessions,
            ILogger<TrackingCoordinator> logger)
        {
            _foreground = foreground;
            _idle = idle;
            _rules = rules;
            _sessions = sessions;
            _logger = logger;
        }

        public async Task RunAsync(TimeSpan pollInterval, TimeSpan idleThreshold, CancellationToken ct)
        {
            _logger.LogInformation("TrackingCoordinator started. Poll={Poll}s IdleThreshold={Idle}s",
                pollInterval.TotalSeconds, idleThreshold.TotalSeconds);

            while (!ct.IsCancellationRequested)
            {
                var fg = _foreground.GetCurrentForegroundApp();

                // If we have no foreground window, treat as "no game"
                var exe = fg?.ProcessName;

                long? matchedGameId = null;
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    matchedGameId = await _rules.MatchGameIdByExeAsync(exe);
                }

                // Handle game change / exit
                if (matchedGameId != _activeGameId)
                {
                    // Stop previous session if any
                    if (_activeSessionId.HasValue)
                    {
                        await _sessions.StopSessionAsync(_activeSessionId.Value, _idleSecondsAccumulated);
                        _logger.LogInformation("Session stopped. SessionId={SessionId} IdleSeconds={Idle}",
                            _activeSessionId.Value, _idleSecondsAccumulated);
                    }

                    _activeGameId = matchedGameId;
                    _activeSessionId = null;
                    _idleSecondsAccumulated = 0;

                    // Start new session if a game is matched
                    if (_activeGameId.HasValue)
                    {
                        _activeSessionId = await _sessions.StartSessionAsync(_activeGameId.Value, source: "focus");
                        _logger.LogInformation("Session started. GameId={GameId} SessionId={SessionId} Exe={Exe}",
                            _activeGameId.Value, _activeSessionId.Value, exe);
                    }
                }

                // If session active, accumulate idle
                if (_activeSessionId.HasValue)
                {
                    var idle = _idle.GetIdleTime();
                    if (idle >= idleThreshold)
                    {
                        _idleSecondsAccumulated += (int)pollInterval.TotalSeconds;
                        _logger.LogDebug("Idle accumulating. SessionId={SessionId} IdleNow={IdleSeconds}s TotalIdle={TotalIdle}s",
                            _activeSessionId.Value, (int)idle.TotalSeconds, _idleSecondsAccumulated);
                    }
                }

                await Task.Delay(pollInterval, ct);
            }

            // Stop active session on shutdown
            if (_activeSessionId.HasValue)
            {
                await _sessions.StopSessionAsync(_activeSessionId.Value, _idleSecondsAccumulated);
                _logger.LogInformation("Session stopped on shutdown. SessionId={SessionId} IdleSeconds={Idle}",
                    _activeSessionId.Value, _idleSecondsAccumulated);
            }
        }
    }
}
