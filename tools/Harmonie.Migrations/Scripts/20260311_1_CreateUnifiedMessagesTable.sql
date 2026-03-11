-- Migration: Create unified messages table
-- Date: 2026-03-11
-- Description: Replaces channel_messages and direct_messages with a single polymorphic messages table

CREATE TABLE IF NOT EXISTS messages (
    id UUID PRIMARY KEY,
    channel_id UUID NULL REFERENCES guild_channels(id) ON DELETE CASCADE,
    conversation_id UUID NULL REFERENCES conversations(id) ON DELETE CASCADE,
    author_user_id UUID NOT NULL REFERENCES users(id),
    content VARCHAR(4000) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL,
    updated_at_utc TIMESTAMPTZ NULL,
    deleted_at_utc TIMESTAMPTZ NULL,
    search_vector tsvector,
    CONSTRAINT chk_messages_exactly_one_parent
        CHECK (
            (channel_id IS NOT NULL AND conversation_id IS NULL)
            OR (channel_id IS NULL AND conversation_id IS NOT NULL)
        )
);

CREATE OR REPLACE FUNCTION set_message_search_vector()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.search_vector := to_tsvector('simple', COALESCE(NEW.content, ''));
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_messages_search_vector ON messages;

CREATE TRIGGER trg_messages_search_vector
BEFORE INSERT OR UPDATE OF content
ON messages
FOR EACH ROW
EXECUTE FUNCTION set_message_search_vector();

CREATE INDEX IF NOT EXISTS idx_messages_channel_created_active
    ON messages(channel_id, created_at_utc DESC, id DESC)
    WHERE channel_id IS NOT NULL AND deleted_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS idx_messages_conversation_created_active
    ON messages(conversation_id, created_at_utc DESC, id DESC)
    WHERE conversation_id IS NOT NULL AND deleted_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS idx_messages_search_vector_active
    ON messages
    USING GIN (search_vector)
    WHERE deleted_at_utc IS NULL;

COMMENT ON TABLE messages IS 'Unified store for guild channel and direct conversation messages';
COMMENT ON COLUMN messages.search_vector IS 'Full-text search vector derived from message content';
