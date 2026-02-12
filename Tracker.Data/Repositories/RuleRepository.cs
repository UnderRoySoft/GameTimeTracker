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
        /// Returns GameId if executable_name matches; otherwise null.
        /// </summary>
        public async Task<long?> MatchGameIdByExeAsync(string executableName)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            return await conn.ExecuteScalarAsync<long?>(
                @"SELECT game_id
                  FROM executable_rules
                  WHERE LOWER(executable_name) = LOWER(@exe)
                  ORDER BY priority DESC
                  LIMIT 1;",
                new { exe = executableName });
        }

        public async Task<IReadOnlyList<(long RuleId, string GameName, string ExecutableName, int Priority)>> ListRulesWithGameAsync()
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync<(long RuleId, string GameName, string ExecutableName, int Priority)>(
                @"SELECT
                      r.id AS RuleId,
                      g.name AS GameName,
                      r.executable_name AS ExecutableName,
                      r.priority AS Priority
                  FROM executable_rules r
                  JOIN games g ON g.id = r.game_id
                  ORDER BY r.priority DESC, g.name ASC, r.executable_name ASC;");

            return rows.AsList();
        }
    }
}
