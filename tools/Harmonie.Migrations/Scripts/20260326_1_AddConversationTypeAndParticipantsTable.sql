-- Migration: Add conversation type/name columns and create conversation_participants table
-- Date: 2026-03-26
-- Description: Prepares the schema for group conversations by adding a type discriminator,
--              an optional name, and a join table for participants.
--              Existing DM conversations are backfilled into conversation_participants.

ALTER TABLE conversations
    ADD COLUMN type VARCHAR(7)    NOT NULL DEFAULT 'direct',
    ADD COLUMN name VARCHAR(100)  NULL;

CREATE TABLE conversation_participants (
    conversation_id UUID        NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    user_id         UUID        NOT NULL REFERENCES users(id),
    joined_at_utc   TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (conversation_id, user_id)
);

CREATE INDEX ix_cp_user_id ON conversation_participants(user_id);

-- Backfill: insert both participants from existing DM conversations
INSERT INTO conversation_participants (conversation_id, user_id, joined_at_utc)
SELECT id, user1_id, created_at_utc FROM conversations
UNION ALL
SELECT id, user2_id, created_at_utc FROM conversations;
