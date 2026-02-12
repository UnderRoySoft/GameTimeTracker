using System;
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

        public async Task<long> StartSessionAsync(long gameId, string source)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var now = DateTimeOffset.UtcNow.ToString("O");
            await conn.ExecuteAsync(
                @"INSERT INTO sessions(game_id, start_utc, end_utc, idle_seconds, source, created_utc)
                  VALUES (@gameId, @startUtc, NULL, 0, @source, @createdUtc);",
                new { gameId, startUtc = now, source, createdUtc = now });

            return await conn.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }

        public async Task StopSessionAsync(long sessionId, int idleSeconds)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync();

            var end = DateTimeOffset.UtcNow.ToString("O");

            await conn.ExecuteAsync(
                @"UPDATE sessions
                  SET end_utc = @endUtc,
                      idle_seconds = @idleSeconds
                  WHERE id = @id;",
                new { id = sessionId, endUtc = end, idleSeconds });
        }
        
        public async Task<IReadOnlyList<(long GameId, string GameName, long ActiveSeconds, long IdleSeconds)>> GetTotalsForUtcDayAsync(DateTime utcDay)
        {
            // utcDay should be a UTC date (00:00:00).
            var dayStart = new DateTimeOffset(utcDay, TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);

            await using var conn = _factory.Create();
            await conn.OpenAsync();

            // We count only time within [dayStart, dayEnd).
            // If end_utc is NULL (active session), we treat it as "now".
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
            WHEN s.start_utc >= @dayStart AND s.start_utc < @dayEnd THEN s.idle_seconds
            ELSE 0
        END
    ) AS INTEGER) AS IdleSeconds
FROM sessions s
JOIN games g ON g.id = s.game_id
WHERE s.start_utc < @dayEnd
  AND COALESCE(s.end_utc, @nowUtc) > @dayStart
GROUP BY s.game_id, g.name
ORDER BY ActiveSeconds DESC;
";

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
    }
}
