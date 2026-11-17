-- AERON Milestone 1 Foundation Schema (Version 1)
-- Copyright (c) 2026 Avenox Studio. All Rights Reserved.

CREATE TABLE IF NOT EXISTS app_instance_meta (
    key TEXT PRIMARY KEY NOT NULL,
    value TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS lifecycle_journal (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    instance_id TEXT NOT NULL,
    state TEXT NOT NULL,
    reason TEXT NOT NULL,
    occurred_at_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_lifecycle_journal_occurred_at_utc
    ON lifecycle_journal (occurred_at_utc DESC);
