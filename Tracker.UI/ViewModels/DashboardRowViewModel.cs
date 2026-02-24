using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tracker.UI.ViewModels
{
    public partial class DashboardRowViewModel : ObservableObject
    {
        [ObservableProperty]
        private long gameId;

        [ObservableProperty]
        private string gameName = string.Empty;

        [ObservableProperty]
        private long activeSeconds;

        [ObservableProperty]
        private long idleSeconds;

        public string ActiveFormatted =>
            TimeSpan.FromSeconds(ActiveSeconds).ToString(@"hh\:mm\:ss");

        public string IdleFormatted =>
            TimeSpan.FromSeconds(IdleSeconds).ToString(@"hh\:mm\:ss");
    }
}