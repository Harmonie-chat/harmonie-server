-- Migration: Add full-text search support to channel_messages
-- Date: 2026-03-08
-- Description: Adds a search_vector column with trigger maintenance, GIN index, and backfill for existing rows

ALTER TABLE channel_messages
    ADD COLUMN IF NOT EXISTS search_vector tsvector;

CREATE OR REPLACE FUNCTION set_channel_message_search_vector()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.search_vector := to_tsvector('simple', COALESCE(NEW.content, ''));
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_channel_messages_search_vector ON channel_messages;

CREATE TRIGGER trg_channel_messages_search_vector
BEFORE INSERT OR UPDATE OF content
ON channel_messages
FOR EACH ROW
EXECUTE FUNCTION set_channel_message_search_vector();

UPDATE channel_messages
SET search_vector = to_tsvector('simple', COALESCE(content, ''))
WHERE search_vector IS NULL;

CREATE INDEX IF NOT EXISTS idx_channel_messages_search_vector_active
    ON channel_messages
    USING GIN (search_vector)
    WHERE deleted_at_utc IS NULL;

COMMENT ON COLUMN channel_messages.search_vector IS 'Full-text search vector derived from message content';
