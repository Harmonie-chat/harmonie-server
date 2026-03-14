ALTER TABLE users
    ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'online',
    ADD COLUMN status_updated_at_utc TIMESTAMP;

-- Backfill: all existing active users default to 'online'
-- No-op since DEFAULT handles new rows and existing rows get 'online' from the ALTER.
