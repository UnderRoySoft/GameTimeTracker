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

        // For throttled “alive” logging
        private DateTimeOffset _lastHeartbeatUtc = DateTimeOffset.MinValue;

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
            _logger.LogInformation(
                "TrackingCoordinator started. Poll={Poll}s IdleThreshold={Idle}s",
                pollInterval.TotalSeconds, idleThreshold.TotalSeconds);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Heartbeat every ~10 seconds so you can confirm the loop is alive in publish logs
                    var nowUtc = DateTimeOffset.UtcNow;
                    if ((nowUtc - _lastHeartbeatUtc).TotalSeconds >= 10)
                    {
                        _lastHeartbeatUtc = nowUtc;
                        _logger.LogInformation(
                            "Heartbeat. ActiveGameId={GameId} ActiveSessionId={SessionId} TotalIdle={Idle}s",
                            _activeGameId, _activeSessionId, _idleSecondsAccumulated);
                    }

                    var fg = _foreground.GetCurrentForegroundApp();

                    // Foreground might be null (no window)
                    var rawExe = fg?.ProcessName;

                    // Normalize exe to match rules (rules expect "something.exe")
                    var exe = NormalizeExeName(rawExe);

                    long? matchedGameId = null;
                    if (!string.IsNullOrWhiteSpace(exe))
                    {
                        matchedGameId = await _rules.MatchGameIdByExeAsync(exe);

                        // Helpful when debugging rule mismatches
                        _logger.LogDebug("Foreground exe detected. Raw={RawExe} Normalized={Exe} MatchedGameId={GameId}",
                            rawExe, exe, matchedGameId);
                    }
                    else
                    {
                        // Optional: helps when you see constant null foregrounds in publish
                        _logger.LogDebug("No foreground exe detected (raw was null/empty).");
                    }

                    // Handle game change / exit
                    if (matchedGameId != _activeGameId)
                    {
                        // Stop previous session if any
                        if (_activeSessionId.HasValue)
                        {
                            await _sessions.StopSessionAsync(_activeSessionId.Value, _idleSecondsAccumulated);

                            _logger.LogInformation(
                                "Session stopped. PrevGameId={GameId} SessionId={SessionId} IdleSeconds={Idle}",
                                _activeGameId, _activeSessionId.Value, _idleSecondsAccumulated);
                        }

                        _activeGameId = matchedGameId;
                        _activeSessionId = null;
                        _idleSecondsAccumulated = 0;

                        // Start new session if a game is matched
                        if (_activeGameId.HasValue)
                        {
                            _activeSessionId = await _sessions.StartSessionAsync(_activeGameId.Value, source: "focus");

                            _logger.LogInformation(
                                "Session started. GameId={GameId} SessionId={SessionId} Exe={Exe}",
                                _activeGameId.Value, _activeSessionId.Value, exe);
                        }
                        else
                        {
                            _logger.LogInformation("No matched game. Tracking idle (no active session). Exe={Exe}", exe);
                        }
                    }

                    // If session active, accumulate idle
                    if (_activeSessionId.HasValue)
                    {
                        var idle = _idle.GetIdleTime();
                        if (idle >= idleThreshold)
                        {
                            // Accumulate only whole seconds from poll interval
                            _idleSecondsAccumulated += (int)Math.Max(0, pollInterval.TotalSeconds);

                            _logger.LogDebug(
                                "Idle accumulating. SessionId={SessionId} IdleNow={IdleSeconds}s TotalIdle={TotalIdle}s",
                                _activeSessionId.Value, (int)idle.TotalSeconds, _idleSecondsAccumulated);
                        }
                    }

                    // IMPORTANT: Task.Delay(pollInterval, ct) throws when ct is cancelled.
                    // We catch it so we can gracefully stop the active session below.
                    try
                    {
                        await Task.Delay(pollInterval, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Let UiTrackingHost see it too (it logs Task fault). This also logs in ILogger pipeline.
                _logger.LogError(ex, "TrackingCoordinator crashed.");
                throw;
            }
            finally
            {
                // Stop active session on shutdown (always runs now)
                if (_activeSessionId.HasValue)
                {
                    try
                    {
                        await _sessions.StopSessionAsync(_activeSessionId.Value, _idleSecondsAccumulated);
                        _logger.LogInformation(
                            "Session stopped on shutdown. SessionId={SessionId} IdleSeconds={Idle}",
                            _activeSessionId.Value, _idleSecondsAccumulated);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed stopping session on shutdown. SessionId={SessionId}", _activeSessionId.Value);
                    }
                }

                _logger.LogInformation("TrackingCoordinator stopped.");
            }
        }

        private static string? NormalizeExeName(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return null;

            var trimmed = processName.Trim();

            // Some APIs already return "something.exe", others return "something"
            if (!trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                trimmed += ".exe";

            return trimmed;
        }
    }
}