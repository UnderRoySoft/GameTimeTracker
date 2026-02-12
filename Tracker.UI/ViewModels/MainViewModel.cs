namespace Tracker.UI.ViewModels
{
    public sealed class MainViewModel
    {
        public DashboardViewModel Dashboard { get; }
        public RulesViewModel Rules { get; }

        public MainViewModel(DashboardViewModel dashboard, RulesViewModel rules)
        {
            Dashboard = dashboard;
            Rules = rules;
        }
    }
}
