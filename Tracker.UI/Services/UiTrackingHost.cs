using System;
using System.Threading;
using System.Threading.Tasks;
using Tracker.Core.Tracking;

namespace Tracker.UI.Services
{
    public sealed class UiTrackingHost : IDisposable
    {
        private readonly TrackingCoordinator _coordinator;
        private CancellationTokenSource? _cts;
        private Task? _task;

        public UiTrackingHost(TrackingCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public void Start()
        {
            // Prevent double-start (can happen if startup is called twice in edge cases)
            if (_task is { IsCompleted: false })
            {
                Diag.Write("UiTrackingHost.Start(): already running, ignoring Start()");
                return;
            }

            // Clean up previous run if any
            try { _cts?.Dispose(); } catch { /* ignore */ }

            _cts = new CancellationTokenSource();

            Diag.Write("UiTrackingHost.Start(): creating tracking task...");

            _task = _coordinator.RunAsync(
                pollInterval: TimeSpan.FromSeconds(1),
                idleThreshold: TimeSpan.FromSeconds(120),
                ct: _cts.Token);

            // IMPORTANT: observe exceptions ASAP so publish doesn't fail silently
            _task.ContinueWith(t =>
            {
                if (t.IsCanceled)
                {
                    Diag.Write("TrackingCoordinator task: CANCELED");
                    return;
                }

                if (t.IsFaulted)
                {
                    Diag.Write("TrackingCoordinator task: FAULTED");
                    Diag.Write(t.Exception?.ToString() ?? "(no exception details)");
                    return;
                }

                Diag.Write("TrackingCoordinator task: COMPLETED (unexpected if app still running)");
            }, TaskScheduler.Default);

            Diag.Write("UiTrackingHost.Start(): tracking task started");
        }

        public async Task StopAsync()
        {
            if (_cts == null)
            {
                Diag.Write("UiTrackingHost.StopAsync(): no CTS (not running)");
                return;
            }

            try
            {
                Diag.Write("UiTrackingHost.StopAsync(): cancel requested");
                _cts.Cancel();
            }
            catch (Exception ex)
            {
                Diag.Write("UiTrackingHost.StopAsync(): cancel FAILED: " + ex);
            }

            if (_task != null)
            {
                try
                {
                    Diag.Write("UiTrackingHost.StopAsync(): awaiting tracking task...");
                    await _task.ConfigureAwait(false);
                    Diag.Write("UiTrackingHost.StopAsync(): tracking task ended cleanly");
                }
                catch (OperationCanceledException)
                {
                    Diag.Write("UiTrackingHost.StopAsync(): task canceled (expected)");
                }
                catch (Exception ex)
                {
                    // DO NOT swallow — log it
                    Diag.Write("UiTrackingHost.StopAsync(): task ended with exception: " + ex);
                }
            }
        }

        public void Dispose()
        {
            try { _cts?.Dispose(); } catch { /* ignore */ }
        }
    }
}