using System;
using System.IO;

namespace Tracker.Data.Infrastructure
{
    public static class DbPaths
    {
        /// <summary>
        /// Returns a stable per-user app data folder, e.g.
        /// C:\Users\user\AppData\Local\GameTimeTracker
        /// </summary>
        public static string GetAppDataDirectory()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(baseDir, "GameTimeTracker");
            Directory.CreateDirectory(appDir);
            return appDir;
        }

        public static string GetDatabaseFilePath()
        {
            return Path.Combine(GetAppDataDirectory(), "gametime-tracker.sqlite");
        }
    }
}
