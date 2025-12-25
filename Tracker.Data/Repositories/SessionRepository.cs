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
    }
}
