using System;
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

        // Create game if missing, otherwise return existing id (case-insensitive)
        public async Task<long> CreateOrGetAsync(string gameName)
        {
            var name = (gameName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Game name is empty.", nameof(gameName));

            await using var conn = _factory.Create();
            await conn.OpenAsync();

            // Try find existing (NOCASE)
            var existing = await conn.ExecuteScalarAsync<long?>(@"
SELECT id
FROM games
WHERE name = @name COLLATE NOCASE
LIMIT 1;", new { name });

            if (existing.HasValue)
                return existing.Value;

            var now = DateTimeOffset.UtcNow.ToString("O");

            await conn.ExecuteAsync(@"
INSERT INTO games(name, created_utc)
VALUES (@name, @createdUtc);", new { name, createdUtc = now });

            return await conn.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
        }

        /// <summary>
        /// Deletes the game AND all dependent data (rules + sessions) in a transaction.
        /// This avoids FK errors and keeps DB consistent.
        /// </summary>
        public async Task DeleteGameCascadeAsync(long gameId)
        {
            if (gameId <= 0) return;

            await using var conn = _factory.Create();
            await conn.OpenAsync();

            // Ensure FK enforcement on this connection
            await conn.ExecuteAsync("PRAGMA foreign_keys = ON;");

            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                // Delete dependents first
                await conn.ExecuteAsync(
                    @"DELETE FROM executable_rules WHERE game_id = @gameId;",
                    new { gameId }, tx);

                await conn.ExecuteAsync(
                    @"DELETE FROM sessions WHERE game_id = @gameId;",
                    new { gameId }, tx);

                // Then delete the game
                await conn.ExecuteAsync(
                    @"DELETE FROM games WHERE id = @gameId;",
                    new { gameId }, tx);

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}