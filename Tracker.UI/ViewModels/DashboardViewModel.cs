using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Tracker.Data.Repositories;

namespace Tracker.UI.ViewModels
{
    public sealed partial class DashboardViewModel : ObservableObject
    {
        private readonly SessionRepository _sessions;

        public ObservableCollection<GameTotalRow> TodayTotals { get; } = new();

        [ObservableProperty]
        private string _status = "Ready";

        public DashboardViewModel(SessionRepository sessions)
        {
            _sessions = sessions;
        }

        public async Task LoadAsync()
        {
            Status = "Loading…";
            TodayTotals.Clear();

            // Use UTC day for now (simple + consistent with DB).
            var utcDay = DateTime.UtcNow.Date;

            var rows = await _sessions.GetTotalsForUtcDayAsync(utcDay);

            foreach (var r in rows)
            {
                TodayTotals.Add(new GameTotalRow(
                    Game: r.GameName,
                    Active: FormatSeconds(r.ActiveSeconds),
                    Idle: FormatSeconds(r.IdleSeconds)
                ));
            }

            Status = $"Loaded {TodayTotals.Count} game(s) for today (UTC).";
        }

        private static string FormatSeconds(long seconds)
        {
            if (seconds < 0) seconds = 0;
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }
    }
}
