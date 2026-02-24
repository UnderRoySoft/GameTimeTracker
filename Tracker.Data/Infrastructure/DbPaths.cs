using System;
using System.IO;

namespace Tracker.Data.Infrastructure
{
    public static class DbPaths
    {
        private const string AppFolderName = "GameTimeTracker";
        private const string DbFileName = "gametime-tracker.sqlite";

        public static string GetAppDataFolder()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(root, AppFolderName);
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string GetDatabaseFilePath()
        {
            return Path.Combine(GetAppDataFolder(), DbFileName);
        }
    }
}
