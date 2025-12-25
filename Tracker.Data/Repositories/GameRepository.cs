using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Tracker.Data.Infrastructure;

namespace Tracker.Data.Repositories
{
    public sealed class GameRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public GameRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<long> CreateOrGetAsync(string name)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            // Try get existing
            var existing = await conn.ExecuteScalarAsync<long?>(
                "SELECT id FROM games WHERE name = @name LIMIT 1;",
                new { name });

            if (existing.HasValue)
                return existing.Value;

            // Insert new
            var now = DateTimeOffset.UtcNow.ToString("O");
            await conn.ExecuteAsync(
                "INSERT INTO games(name, created_utc) VALUES (@name, @createdUtc);",
                new { name, createdUtc = now });

            var id = await conn.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
            return id;
        }

        public async Task<IReadOnlyList<(long Id, string Name)>> ListAsync()
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync<(long Id, string Name)>(
                "SELECT id AS Id, name AS Name FROM games ORDER BY name ASC;");

            return rows.AsList();
        }
    }
}
