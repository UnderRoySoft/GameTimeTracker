using System;
using System.IO;
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
using Tracker.UI.Services;
using Tracker.UI.ViewModels;

namespace Tracker.UI
{
    public partial class App : Application
    {
        private IHost? _host;
        private TaskbarIcon? _trayIcon;
        private UiTrackingHost? _trackingHost;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Single source of truth for DB path (prevents “DB A in DI, DB B in UI” bugs)
            var dbPath = DbPaths.GetDatabaseFilePath();

            // Global crash capture (publish often hides background-task exceptions)
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Diag.Write("UnhandledException: " + args.ExceptionObject);
            };

            DispatcherUnhandledException += (_, args) =>
            {
                Diag.Write("DispatcherUnhandledException: " + args.Exception);
                // Keep app alive to allow reading logs
                args.Handled = true;
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Diag.Write("UnobservedTaskException: " + args.Exception);
                args.SetObserved();
            };

            // Startup diagnostics
            Diag.Write("=== APP START ===");
            Diag.Write($"BaseDirectory={AppDomain.CurrentDomain.BaseDirectory}");
            Diag.Write($"CurrentDirectory={Environment.CurrentDirectory}");
            Diag.Write($"LocalAppData={Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
            Diag.Write($"DB Path={dbPath}");
            Diag.Write($"DB Exists Before Init={File.Exists(dbPath)}");

            try
            {
                _host = Host.CreateDefaultBuilder()
                    .ConfigureServices(services =>
                    {
                        // Core tracking
                        services.AddSingleton<IForegroundAppDetector, ForegroundAppDetector>();
                        services.AddSingleton<InputIdleDetector>();
                        services.AddSingleton<TrackingCoordinator>();

                        // Data
                        services.AddSingleton(new SqliteConnectionFactory(dbPath));
                        services.AddSingleton<DbInitializer>();
                        services.AddSingleton<GameRepository>();
                        services.AddSingleton<SessionRepository>();
                        services.AddSingleton<RuleRepository>();

                        // UI
                        services.AddSingleton<DashboardViewModel>();
                        services.AddSingleton<RulesViewModel>();
                        services.AddSingleton<MainViewModel>();
                        services.AddSingleton<MainWindow>();

                        // Background runner
                        services.AddSingleton<UiTrackingHost>();
                    })
                    .Build();

                // Optional: keep your MessageBox, but now it matches the DI dbPath
                MessageBox.Show(dbPath, "DB path used by this app");

                // Ensure DB exists + schema
                Diag.Write("DB init: starting...");
                _host.Services.GetRequiredService<DbInitializer>()
                    .InitializeAsync().GetAwaiter().GetResult();
                Diag.Write("DB init: SUCCESS");

                Diag.Write($"DB Exists After Init={File.Exists(dbPath)}");
                if (File.Exists(dbPath))
                {
                    var fi = new FileInfo(dbPath);
                    Diag.Write($"DB SizeBytes={fi.Length}");
                    Diag.Write($"DB LastWriteUtc={fi.LastWriteTimeUtc:O}");
                }

                // Start tracking in background
                _trackingHost = _host.Services.GetRequiredService<UiTrackingHost>();
                Diag.Write("TrackingHost: Start() calling...");
                _trackingHost.Start();
                Diag.Write("TrackingHost: Start() returned (no exception)");

                // Tray icon (file-based)
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "tray.ico");
                Diag.Write($"Tray icon path={iconPath} Exists={File.Exists(iconPath)}");

                var icon = File.Exists(iconPath)
                    ? new System.Drawing.Icon(iconPath)
                    : System.Drawing.SystemIcons.Application;

                _trayIcon = new TaskbarIcon
                {
                    ToolTipText = "GameTimeTracker",
                    Icon = icon,
                    ContextMenu = BuildTrayMenu()
                };

                // Show UI
                var window = _host.Services.GetRequiredService<MainWindow>();
                MainWindow = window;
                window.Show();

                Diag.Write("MainWindow: shown");
            }
            catch (Exception ex)
            {
                Diag.Write("Startup crash: " + ex);
                MessageBox.Show(ex.ToString(), "Startup crash");
                Shutdown();
            }
        }

        private ContextMenu BuildTrayMenu()
        {
            var menu = new ContextMenu();

            var open = new MenuItem { Header = "Open Dashboard" };
            open.Click += (_, __) =>
            {
                if (MainWindow == null) return;
                MainWindow.Show();
                MainWindow.WindowState = WindowState.Normal;
                MainWindow.Activate();
            };

            var exit = new MenuItem { Header = "Exit" };
            exit.Click += async (_, __) =>
            {
                try
                {
                    Diag.Write("Exit clicked: stopping tracking host...");
                    if (_trackingHost != null)
                        await _trackingHost.StopAsync();
                    Diag.Write("Exit clicked: tracking host stopped");
                }
                catch (Exception ex)
                {
                    Diag.Write("Exit clicked: StopAsync FAILED: " + ex);
                }

                Diag.Write("Exit clicked: Shutdown()");
                Shutdown();
            };

            menu.Items.Add(open);
            menu.Items.Add(new Separator());
            menu.Items.Add(exit);

            return menu;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Diag.Write("OnExit: disposing tray icon + host...");
                _trayIcon?.Dispose();
                _host?.Dispose();
                Diag.Write("OnExit: done");
            }
            catch (Exception ex)
            {
                Diag.Write("OnExit error: " + ex);
            }

            base.OnExit(e);
        }
    }
}