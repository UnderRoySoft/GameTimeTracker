// MainWindow.xaml.cs
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Tracker.UI.ViewModels;

namespace Tracker.UI
{
    public partial class MainWindow : Window
    {
        private bool _initialLoadDone;

        // Auto-refresh timer for Dashboard
        private readonly DispatcherTimer _dashboardTimer = new();

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            Loaded += MainWindow_Loaded;

            // Hide instead of closing (so tracker keeps running in tray)
            Closing += MainWindow_Closing;

            // Auto-refresh Dashboard every 5 seconds (only when Dashboard tab is selected)
            _dashboardTimer.Interval = TimeSpan.FromSeconds(5);
            _dashboardTimer.Tick += DashboardTimer_Tick;
            _dashboardTimer.Start();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialLoadDone) return;
            _initialLoadDone = true;

            if (DataContext is not MainViewModel vm) return;

            await SafeRun(async () =>
            {
                await vm.Dashboard.LoadAsync();
                await vm.Rules.LoadAsync();
                vm.UiStatus = $"Loaded: {DateTime.Now:HH:mm:ss}";
            });
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private async void DashboardRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            await SafeRun(async () =>
            {
                await vm.Dashboard.LoadAsync();
                vm.UiStatus = $"Dashboard refreshed: {DateTime.Now:HH:mm:ss}";
            });
        }

        private async void RulesRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            await SafeRun(async () =>
            {
                await vm.Rules.LoadAsync();
                vm.UiStatus = $"Rules refreshed: {DateTime.Now:HH:mm:ss}";
            });
        }

        // When user switches tabs, refresh the selected one
        private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not TabControl) return;
            if (DataContext is not MainViewModel vm) return;

            if (sender is TabControl tc && tc.SelectedItem is TabItem tab && tab.Header is string header)
            {
                if (header == "Dashboard")
                {
                    await SafeRun(async () =>
                    {
                        await vm.Dashboard.LoadAsync();
                        vm.UiStatus = $"Dashboard auto-refreshed: {DateTime.Now:HH:mm:ss}";
                    });
                }
                else if (header == "Rules")
                {
                    await SafeRun(async () =>
                    {
                        await vm.Rules.LoadAsync();
                        vm.UiStatus = $"Rules auto-refreshed: {DateTime.Now:HH:mm:ss}";
                    });
                }
            }
        }

        private async void DashboardTimer_Tick(object? sender, EventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;

            // Refresh only while Dashboard tab is selected
            if (MainTabControl?.SelectedItem is TabItem tab && tab.Header?.ToString() == "Dashboard")
            {
                await SafeRun(async () =>
                {
                    await vm.Dashboard.LoadAsync();
                    vm.UiStatus = $"Dashboard auto-refreshed: {DateTime.Now:HH:mm:ss}";
                });
            }
        }

        private static async Task SafeRun(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "UI action failed");
            }
        }
    }
}