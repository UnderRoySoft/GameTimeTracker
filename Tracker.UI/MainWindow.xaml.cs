using System.Windows;
using Tracker.UI.ViewModels;

namespace Tracker.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            Loaded += async (_, __) =>
            {
                await vm.Dashboard.LoadAsync();
                await vm.Rules.LoadAsync();
            };
        }

        private async void DashboardRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.Dashboard.LoadAsync();
        }

        private async void RulesRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.Rules.LoadAsync();
        }
    }
}
