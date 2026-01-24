using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Tracker.Core.Abstractions;
using Tracker.Core.Models;

namespace Tracker.Core.Monitoring
{
    public sealed class ForegroundAppDetector : IForegroundAppDetector
    {
        public ForegroundAppInfo? GetCurrentForegroundApp()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return null;

                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == 0)
                    return null;

                var title = GetWindowTitle(hwnd);

                using var proc = Process.GetProcessById((int)pid);
                var exeName = SafeExeName(proc);
                var filePath = SafeMainModulePath(proc);

                return new ForegroundAppInfo(
                    ProcessId: (int)pid,
                    ProcessName: exeName,
                    FilePath: filePath,
                    WindowTitle: title,
                    ObservedAtUtc: DateTimeOffset.UtcNow
                );
            }
            catch
            {
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

        private static string SafeExeName(Process proc)
        {
            var name = proc.ProcessName;
            if (string.IsNullOrWhiteSpace(name))
                return "unknown.exe";

            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".exe";
        }

        private static string? SafeMainModulePath(Process proc)
        {
            try
            {
                var path = proc.MainModule?.FileName;
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
