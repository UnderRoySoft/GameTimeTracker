using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Tracker.Data.Infrastructure;

namespace Tracker.Data.Repositories
{
    public sealed class RuleRepository
    {
        private readonly SqliteConnectionFactory _factory;
        private readonly ILogger<RuleRepository> _logger;

        public RuleRepository(SqliteConnectionFactory factory, ILogger<RuleRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<long> AddRuleAsync(long gameId, string executableName, int priority = 100)
        {
            var normalizedExe = NormalizeExe(executableName);

            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var now = DateTimeOffset.UtcNow.ToString("O");

            _logger.LogInformation("AddRule: GameId={GameId} ExeRaw='{Raw}' ExeNormalized='{Exe}' Priority={Priority}",
                gameId, executableName, normalizedExe, priority);

            await conn.ExecuteAsync(
                @"INSERT INTO executable_rules(game_id, executable_name, path_pattern, window_title_pattern, priority, created_utc)
                  VALUES (@gameId, @exe, NULL, NULL, @priority, @createdUtc);",
                new { gameId, exe = normalizedExe, priority, createdUtc = now });

            return await conn.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }

        public async Task DeleteRuleAsync(long ruleId)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            await conn.ExecuteAsync(
                @"DELETE FROM executable_rules WHERE id = @id;",
                new { id = ruleId });
        }

        public async Task<int> CountRulesAsync()
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            return await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM executable_rules;");
        }

        /// <summary>
        /// Returns GameId if executable_name matches (case-insensitive, trimmed, .exe-normalized); otherwise null.
        /// </summary>
        public async Task<long?> MatchGameIdByExeAsync(string executableName)
        {
            var normalizedExe = NormalizeExe(executableName);

            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var gameId = await conn.ExecuteScalarAsync<long?>(
                @"SELECT game_id
                  FROM executable_rules
                  WHERE TRIM(executable_name) = @exe COLLATE NOCASE
                  ORDER BY priority DESC
                  LIMIT 1;",
                new { exe = normalizedExe });

            _logger.LogDebug("MatchGameIdByExe: Raw='{Raw}' Normalized='{Exe}' -> GameId={GameId}",
                executableName, normalizedExe, gameId);

            return gameId;
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

        private static string NormalizeExe(string exe)
        {
            if (exe == null) return string.Empty;

            var s = exe.Trim();

            // Some people paste full path; keep only file name
            // Example: C:\Games\Witcher3\witcher.exe -> witcher.exe
            try
            {
                // If it contains a directory separator, extract file name.
                if (s.Contains('\\') || s.Contains('/'))
                    s = System.IO.Path.GetFileName(s);
            }
            catch
            {
                // ignore and keep trimmed
            }

            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            if (!s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                s += ".exe";

            return s;
        }
    }
}