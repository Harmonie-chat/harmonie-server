-- Migration: Add full-text search support to direct_messages
-- Date: 2026-03-08
-- Description: Adds a search_vector column with trigger maintenance, GIN index, and backfill for existing direct messages

ALTER TABLE direct_messages
    ADD COLUMN IF NOT EXISTS search_vector tsvector;

CREATE OR REPLACE FUNCTION set_direct_message_search_vector()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.search_vector := to_tsvector('simple', COALESCE(NEW.content, ''));
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_direct_messages_search_vector ON direct_messages;

CREATE TRIGGER trg_direct_messages_search_vector
BEFORE INSERT OR UPDATE OF content
ON direct_messages
FOR EACH ROW
EXECUTE FUNCTION set_direct_message_search_vector();

UPDATE direct_messages
SET search_vector = to_tsvector('simple', COALESCE(content, ''))
WHERE search_vector IS NULL;

CREATE INDEX IF NOT EXISTS idx_direct_messages_search_vector_active
    ON direct_messages
    USING GIN (search_vector)
    WHERE deleted_at_utc IS NULL;

COMMENT ON COLUMN direct_messages.search_vector IS 'Full-text search vector derived from direct message content';
