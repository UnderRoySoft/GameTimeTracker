PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS games (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    name          TEXT NOT NULL,
    created_utc   TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_games_name ON games(name);

CREATE TABLE IF NOT EXISTS executable_rules (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id             INTEGER NOT NULL,
    executable_name     TEXT NOT NULL,      -- e.g. "eldenring.exe"
    path_pattern        TEXT NULL,          -- optional (future)
    window_title_pattern TEXT NULL,         -- optional (future)
    priority            INTEGER NOT NULL DEFAULT 100,
    created_utc         TEXT NOT NULL,
    FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_rules_game_id ON executable_rules(game_id);
CREATE INDEX IF NOT EXISTS ix_rules_exe ON executable_rules(executable_name);

CREATE TABLE IF NOT EXISTS sessions (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id        INTEGER NOT NULL,
    start_utc      TEXT NOT NULL,
    end_utc        TEXT NULL,              -- NULL while session is active
    idle_seconds   INTEGER NOT NULL DEFAULT 0,
    source         TEXT NOT NULL,          -- e.g. "process" / "focus"
    created_utc    TEXT NOT NULL,
    FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_sessions_game_start ON sessions(game_id, start_utc);
