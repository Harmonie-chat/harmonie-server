-- Migration: Add reply_to_message_id to messages
-- Date: 2026-05-03
-- Description: Allows messages to reference another message as a reply via a self-referencing FK.
--              ON DELETE SET NULL ensures a replying message survives if the target is ever hard-deleted.

ALTER TABLE messages
ADD COLUMN IF NOT EXISTS reply_to_message_id UUID NULL
REFERENCES messages(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_messages_reply_to_message_id
    ON messages(reply_to_message_id)
    WHERE reply_to_message_id IS NOT NULL;

COMMENT ON COLUMN messages.reply_to_message_id IS 'FK to the message being replied to (self-reference). Set to NULL when the target is hard-deleted.';
