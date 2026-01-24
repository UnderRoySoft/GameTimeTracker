using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Tracker.Data.Infrastructure;

namespace Tracker.Data.Repositories
{
    public sealed class RuleRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public RuleRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<long> AddRuleAsync(long gameId, string executableName, int priority = 100)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var now = DateTimeOffset.UtcNow.ToString("O");

            await conn.ExecuteAsync(
                @"INSERT INTO executable_rules(game_id, executable_name, path_pattern, window_title_pattern, priority, created_utc)
                  VALUES (@gameId, @exe, NULL, NULL, @priority, @createdUtc);",
                new { gameId, exe = executableName, priority, createdUtc = now });

            return await conn.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }

        public async Task<int> CountRulesAsync()
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            return await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM executable_rules;");
        }

        /// <summary>
        /// Finds matching GameId for a given executable name.
        /// Returns null if no rule matches.
        /// </summary>
        public async Task<long?> MatchGameIdByExeAsync(string executableName)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            // Highest priority wins (lowest number = higher priority if you prefer; here we use DESC for simplicity)
            var gameId = await conn.ExecuteScalarAsync<long?>(
                @"SELECT game_id
                  FROM executable_rules
                  WHERE LOWER(executable_name) = LOWER(@exe)
                  ORDER BY priority DESC
                  LIMIT 1;",
                new { exe = executableName });

            return gameId;
        }

        public async Task<IReadOnlyList<(long Id, long GameId, string ExecutableName, int Priority)>> ListRulesAsync()
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync<(long Id, long GameId, string ExecutableName, int Priority)>(
                @"SELECT id AS Id, game_id AS GameId, executable_name AS ExecutableName, priority AS Priority
                  FROM executable_rules
                  ORDER BY priority DESC, id ASC;");

            return rows.AsList();
        }
    }
}
