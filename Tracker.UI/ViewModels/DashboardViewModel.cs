using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tracker.Data.Repositories;

namespace Tracker.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly SessionRepository _sessions;
        private readonly GameRepository _games;

        public ObservableCollection<DashboardRowViewModel> Games { get; } = new();

        public DashboardViewModel(SessionRepository sessions, GameRepository games)
        {
            _sessions = sessions;
            _games = games;
        }

        public async Task LoadAsync()
        {
            var totals = await _sessions.GetTotalsAllTimeAsync();

            Games.Clear();

            foreach (var t in totals)
            {
                Games.Add(new DashboardRowViewModel
                {
                    GameId = t.GameId,
                    GameName = t.GameName,
                    ActiveSeconds = t.ActiveSeconds,
                    IdleSeconds = t.IdleSeconds
                });
            }
        }

        // ✅ NEW: delete game (and its rules + sessions)
        [RelayCommand]
        private async Task DeleteGameAsync(long gameId)
        {
            try
            {
                if (gameId <= 0) return;

                var confirm = MessageBox.Show(
                    $"Delete game #{gameId} and ALL its sessions + rules?\n\nThis cannot be undone.",
                    "Confirm delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return;

                await _games.DeleteGameCascadeAsync(gameId);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Delete game failed");
            }
        }
    }
}