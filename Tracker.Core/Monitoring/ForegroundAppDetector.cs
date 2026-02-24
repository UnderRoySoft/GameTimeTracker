using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Tracker.Core.Abstractions;
using Tracker.Core.Models;

namespace Tracker.Core.Monitoring
{
    public sealed class ForegroundAppDetector : IForegroundAppDetector
    {
        private readonly ILogger<ForegroundAppDetector> _logger;

        public ForegroundAppDetector(ILogger<ForegroundAppDetector> logger)
        {
            _logger = logger;
        }

        public ForegroundAppInfo? GetCurrentForegroundApp()
        {
            IntPtr hwnd = IntPtr.Zero;
            uint pid = 0;

            try
            {
                hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return null;

                GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0)
                    return null;

                var title = GetWindowTitle(hwnd);

                using var proc = Process.GetProcessById((int)pid);

                var exeName = SafeExeName(proc);
                if (string.IsNullOrWhiteSpace(exeName))
                {
                    _logger.LogDebug("Foreground detected but exe name unavailable. PID={Pid} Title={Title}", pid, title);
                    return null;
                }

                var filePath = SafeMainModulePath(proc);

                return new ForegroundAppInfo(
                    ProcessId: (int)pid,
                    ProcessName: exeName,
                    FilePath: filePath,
                    WindowTitle: title,
                    ObservedAtUtc: DateTimeOffset.UtcNow
                );
            }
            catch (ArgumentException ex)
            {
                // Process may have exited between window->pid and GetProcessById
                _logger.LogDebug(ex, "Foreground process disappeared. PID={Pid}", pid);
                return null;
            }
            catch (Exception ex)
            {
                // DO NOT swallow silently — this is exactly what breaks publish debugging
                _logger.LogWarning(ex, "GetCurrentForegroundApp failed. HWND={Hwnd} PID={Pid}", hwnd, pid);
                return null;
            }
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            var sb = new StringBuilder(512);
            _ = GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();
            return string.IsNullOrWhiteSpace(title) ? "(no title)" : title;
        }

        private static string? SafeExeName(Process proc)
        {
            try
            {
                var name = proc.ProcessName; // usually returns without ".exe"
                if (string.IsNullOrWhiteSpace(name))
                    return null;

                return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? name.Trim()
                    : (name.Trim() + ".exe");
            }
            catch
            {
                return null;
            }
        }

        private static string? SafeMainModulePath(Process proc)
        {
            try
            {
                var path = proc.MainModule?.FileName; // can throw in some contexts
                if (string.IsNullOrWhiteSpace(path))
                    return null;

                return Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}