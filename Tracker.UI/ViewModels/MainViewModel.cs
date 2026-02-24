using CommunityToolkit.Mvvm.ComponentModel;

namespace Tracker.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public DashboardViewModel Dashboard { get; }
        public RulesViewModel Rules { get; }

        [ObservableProperty]
        private string uiStatus = "Ready";

        public MainViewModel(DashboardViewModel dashboard, RulesViewModel rules)
        {
            Dashboard = dashboard;
            Rules = rules;
        }
    }
}