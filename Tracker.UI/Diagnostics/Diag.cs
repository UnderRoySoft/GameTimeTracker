using System;
using System.Diagnostics;
using System.IO;

namespace Tracker.UI.Diagnostics
{
    internal static class Diag
    {
        private static readonly object _lock = new();

        public static string LogDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "GameTimeTracker", "logs");

        public static string LogFile => Path.Combine(LogDir, "diag.txt");

        public static void Write(string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LogDir);
                    File.AppendAllText(
                        LogFile,
                        $"{DateTimeOffset.UtcNow:O} | PID={Environment.ProcessId} | {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Never crash the app because logging failed.
            }
        }

        public static void DumpEnvironment()
        {
            Write("=== APP START ===");
            Write($"ProcessName={Process.GetCurrentProcess().ProcessName}");
            Write($"BaseDirectory={AppContext.BaseDirectory}");
            Write($"CurrentDirectory={Environment.CurrentDirectory}");
            Write($"LocalAppData={Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
        }

        public static void DumpDbPath(string dbPath)
        {
            Write($"DB Path={dbPath}");
            Write($"DB Exists={File.Exists(dbPath)}");

            if (File.Exists(dbPath))
            {
                var fi = new FileInfo(dbPath);
                Write($"DB SizeBytes={fi.Length}");
                Write($"DB LastWriteUtc={fi.LastWriteTimeUtc:O}");
            }
        }
    }
}