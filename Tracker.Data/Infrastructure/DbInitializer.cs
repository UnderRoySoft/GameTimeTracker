using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;

namespace Tracker.Data.Infrastructure
{
    public sealed class DbInitializer
    {
        private readonly SqliteConnectionFactory _factory;

        // Keep this as the “intended” name, but we’ll also search dynamically.
        private const string InitSqlResourceName = "Tracker.Data.Schema.001_init.sql";

        public DbInitializer(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            // Ensure FK enforcement for this connection.
            await conn.ExecuteAsync("PRAGMA foreign_keys = ON;");

            var sql = await LoadSqlAsync(InitSqlResourceName);
            await conn.ExecuteAsync(sql);

            // Optional but VERY useful: assert that schema actually exists
            await AssertTablesExist(conn);
        }

        private static async Task<string> LoadSqlAsync(string embeddedResourceName)
        {
            var asm = Assembly.GetExecutingAssembly();

            // 1) Try exact name first (fast path)
            var stream = asm.GetManifestResourceStream(embeddedResourceName);

            // 2) If not found, try to locate by suffix (robust to folder/namespace changes)
            if (stream == null)
            {
                var names = asm.GetManifestResourceNames();

                // Find anything that ends with ".001_init.sql" (case-insensitive)
                var candidate = names.FirstOrDefault(n =>
                    n.EndsWith(".001_init.sql", StringComparison.OrdinalIgnoreCase));

                if (candidate != null)
                {
                    stream = asm.GetManifestResourceStream(candidate);
                }

                if (stream == null)
                {
                    // Crash with an extremely actionable message
                    var all = string.Join(Environment.NewLine, names.OrderBy(x => x));
                    throw new InvalidOperationException(
                        $"Embedded SQL resource not found.\n" +
                        $"Tried exact: '{embeddedResourceName}'\n" +
                        $"Also searched for suffix: '.001_init.sql'\n\n" +
                        $"Available embedded resources in '{asm.GetName().Name}':\n{all}\n\n" +
                        $"Fix: set Build Action = Embedded Resource for 001_init.sql, " +
                        $"and ensure its namespace/folder matches the resource name.");
                }
            }

            await using (stream)
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private static async Task AssertTablesExist(System.Data.IDbConnection conn)
        {
            // If schema didn't run, these tables won't exist.
            // This will force a clear failure early.
            var count = await conn.ExecuteScalarAsync<long>(@"
SELECT COUNT(1)
FROM sqlite_master
WHERE type='table'
  AND name IN ('games','executable_rules','sessions');");

            if (count < 3)
            {
                var existing = await conn.QueryAsync<string>(@"
SELECT name
FROM sqlite_master
WHERE type='table'
ORDER BY name;");

                throw new InvalidOperationException(
                    "Database schema initialization did not create expected tables. " +
                    "Expected: games, executable_rules, sessions. " +
                    $"Existing tables: {string.Join(", ", existing)}");
            }
        }
    }
}