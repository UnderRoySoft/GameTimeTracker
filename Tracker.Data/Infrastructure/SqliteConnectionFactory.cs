using Microsoft.Data.Sqlite;

namespace Tracker.Data.Infrastructure
{
    public sealed class SqliteConnectionFactory
    {
        private readonly string _dbPath;

        public SqliteConnectionFactory(string dbPath)
        {
            _dbPath = dbPath;
        }

        public SqliteConnection Create()
        {
            // SQLite will create the file if it does not exist.
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            return new SqliteConnection(cs);
        }
    }
}
