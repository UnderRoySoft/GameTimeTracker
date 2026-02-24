using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Tracker.Data.Infrastructure;

namespace Tracker.Data.Repositories
{
    public sealed class SessionRepository
    {
        private readonly SqliteConnectionFactory _factory;

        public SessionRepository(SqliteConnectionFactory factory)
        {
            _factory = factory;
        }

        // =============================
        // Diagnostics helpers
        // =============================
        public async Task<int> CountSessionsAsync()
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();
            return await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM sessions;");
        }

        public async Task<IReadOnlyList<(long Id, long GameId, string StartUtc, string? EndUtc, int IdleSeconds, string? Source)>> GetLastSessionsAsync(int take = 20)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync<(long Id, long GameId, string StartUtc, string? EndUtc, int IdleSeconds, string? Source)>(@"
SELECT id AS Id,
       game_id AS GameId,
       start_utc AS StartUtc,
       end_utc AS EndUtc,
       COALESCE(idle_seconds, 0) AS IdleSeconds,
       source AS Source
FROM sessions
ORDER BY id DESC
LIMIT @take;", new { take });

            return rows.AsList();
        }

        // =============================
        // Start a session
        // =============================
        public async Task<long> StartSessionAsync(long gameId, string source)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var now = DateTimeOffset.UtcNow.ToString("O");

            await conn.ExecuteAsync(
                @"INSERT INTO sessions(game_id, start_utc, end_utc, idle_seconds, source, created_utc)
                  VALUES (@gameId, @startUtc, NULL, 0, @source, @createdUtc);",
                new
                {
                    gameId,
                    startUtc = now,
                    source,
                    createdUtc = now
                });

            return await conn.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }

        // =============================
        // Stop a session
        // Returns true if a row was updated
        // =============================
        public async Task<bool> StopSessionAsync(long sessionId, int idleSeconds)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var end = DateTimeOffset.UtcNow.ToString("O");

            // IMPORTANT:
            // - don't overwrite end_utc if already stopped
            // - tell caller if nothing was updated (session id wrong / already stopped)
            var affected = await conn.ExecuteAsync(
                @"UPDATE sessions
                  SET end_utc = @endUtc,
                      idle_seconds = @idleSeconds
                  WHERE id = @id
                    AND end_utc IS NULL;",
                new
                {
                    id = sessionId,
                    endUtc = end,
                    idleSeconds
                });

            return affected == 1;
        }

        // =============================
        // Get totals for one UTC day
        // =============================
        public async Task<IReadOnlyList<(long GameId, string GameName, long ActiveSeconds, long IdleSeconds)>>
            GetTotalsForUtcDayAsync(DateTime utcDay)
        {
            var dayStart = new DateTimeOffset(utcDay, TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);

            await using var conn = _factory.Create();
            await conn.OpenAsync();

            // If end_utc is NULL (active session), treat as "now".
            var nowUtc = DateTimeOffset.UtcNow.ToString("O");

            var sql = @"
SELECT
    s.game_id AS GameId,
    g.name    AS GameName,
    CAST(SUM(
        MAX(0,
            (julianday(MIN(COALESCE(s.end_utc, @nowUtc), @dayEnd)) -
             julianday(MAX(s.start_utc, @dayStart))) * 86400.0
        )
    ) AS INTEGER) AS ActiveSeconds,
    CAST(SUM(
        CASE
            WHEN s.start_utc >= @dayStart AND s.start_utc < @dayEnd THEN COALESCE(s.idle_seconds, 0)
            ELSE 0
        END
    ) AS INTEGER) AS IdleSeconds
FROM sessions s
JOIN games g ON g.id = s.game_id
WHERE s.start_utc < @dayEnd
  AND COALESCE(s.end_utc, @nowUtc) > @dayStart
GROUP BY s.game_id, g.name
ORDER BY ActiveSeconds DESC;";

            var rows = await conn.QueryAsync<(long GameId, string GameName, long ActiveSeconds, long IdleSeconds)>(
                sql,
                new
                {
                    dayStart = dayStart.ToString("O"),
                    dayEnd = dayEnd.ToString("O"),
                    nowUtc
                });

            return rows.AsList();
        }

        // =============================
        // Get totals for ALL TIME
        // =============================
        public async Task<IReadOnlyList<(long GameId, string GameName, long ActiveSeconds, long IdleSeconds)>>
            GetTotalsAllTimeAsync()
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var nowUtc = DateTimeOffset.UtcNow.ToString("O");

            var sql = @"
SELECT
    s.game_id AS GameId,
    g.name    AS GameName,
    CAST(SUM(
        MAX(0,
            (julianday(COALESCE(s.end_utc, @nowUtc)) - julianday(s.start_utc)) * 86400.0
        )
    ) AS INTEGER) AS ActiveSeconds,
    CAST(SUM(COALESCE(s.idle_seconds, 0)) AS INTEGER) AS IdleSeconds
FROM sessions s
JOIN games g ON g.id = s.game_id
GROUP BY s.game_id, g.name
ORDER BY ActiveSeconds DESC;";

            var rows = await conn.QueryAsync<(long GameId, string GameName, long ActiveSeconds, long IdleSeconds)>(
                sql,
                new { nowUtc });

            return rows.AsList();
        }
    }
}