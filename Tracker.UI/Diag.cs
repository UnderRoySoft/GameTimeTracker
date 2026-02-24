using System;
using System.IO;

namespace Tracker.UI
{
    internal static class Diag
    {
        private static readonly object _lock = new();

        public static string LogDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameTimeTracker",
                "logs");

        public static string LogPath => Path.Combine(LogDir, "diag.txt");

        public static void Write(string msg)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LogDir);
                    File.AppendAllText(
                        LogPath,
                        $"{DateTimeOffset.UtcNow:O} | PID={Environment.ProcessId} | {msg}{Environment.NewLine}");
                }
            }
            catch
            {
                // Never crash the app because logging failed.
            }
        }
    }
}