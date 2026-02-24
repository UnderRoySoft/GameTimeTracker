using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tracker.Core.Abstractions;
using Tracker.Core.Monitoring;
using Tracker.Core.Services;
using Tracker.Core.Tracking;
using Tracker.Data.Infrastructure;
using Tracker.Data.Repositories;
using Tracker.Tray.Services;

namespace Tracker.Tray
{
    public partial class App : Application
    {
        private IHost? _host;
        private TrayTrackingHost? _runner;
        private TaskbarIcon? _trayIcon;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                _host = Host.CreateDefaultBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton<IForegroundAppDetector, ForegroundAppDetector>();
                        services.AddSingleton<InputIdleDetector>();
                        services.AddSingleton<TrackingCoordinator>();

                        var dbPath = DbPaths.GetDatabaseFilePath();
                        services.AddSingleton(new SqliteConnectionFactory(dbPath));
                        services.AddSingleton<DbInitializer>();
                        services.AddSingleton<GameRepository>();
                        services.AddSingleton<SessionRepository>();
                        services.AddSingleton<RuleRepository>();

                        services.AddSingleton<TrayTrackingHost>();
                    })
                    .Build();

                _runner = _host.Services.GetRequiredService<TrayTrackingHost>();
                await _runner.StartAsync();

                // Load tray icon from disk (Assets/tray.ico next to exe)
                var iconPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets",
                    "tray.ico");

                _trayIcon = new TaskbarIcon
                {
                    ToolTipText = "GameTimeTracker (running)",
                    Icon = new System.Drawing.Icon(iconPath),
                    ContextMenu = BuildMenu()
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Tracker.Tray startup crash");
                Shutdown();
            }
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();

            // OPEN UI
            var openUi = new MenuItem { Header = "Open UI" };
            openUi.Click += (_, __) =>
            {
                try
                {
                    // If UI already running → do nothing
                    if (Process.GetProcessesByName("Tracker.UI").Any())
                        return;

                    var uiPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Tracker.UI.exe");

                    if (File.Exists(uiPath))
                    {
                        Process.Start(new ProcessStartInfo(uiPath)
                        {
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show("Tracker.UI.exe not found in the same folder.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Failed to open UI");
                }
            };

            // EXIT
            var exit = new MenuItem { Header = "Exit" };
            exit.Click += async (_, __) =>
            {
                try
                {
                    if (_runner != null)
                        await _runner.StopAsync();
                }
                catch { }

                Shutdown();
            };

            menu.Items.Add(openUi);
            menu.Items.Add(new Separator());
            menu.Items.Add(exit);

            return menu;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _trayIcon?.Dispose();
                _host?.Dispose();
            }
            catch { }

            base.OnExit(e);
        }
    }
}
