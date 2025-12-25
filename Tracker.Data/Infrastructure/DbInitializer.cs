using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Tracker.Data.Infrastructure
{
    public sealed class DbInitializer
    {
        private readonly SqliteConnectionFactory _factory;

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

            var sql = await LoadSqlAsync("Tracker.Data.Schema.001_init.sql");
            await conn.ExecuteAsync(sql);
        }

        private static async Task<string> LoadSqlAsync(string embeddedResourceName)
        {
            var asm = Assembly.GetExecutingAssembly();
            await using var stream = asm.GetManifestResourceStream(embeddedResourceName)
                ?? throw new InvalidOperationException($"Embedded SQL resource not found: {embeddedResourceName}");

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}
