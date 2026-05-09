-- Migration: Create message mentions table
-- Date: 2026-05-09
-- Description: Stores mentioned user IDs per message for @mention support

CREATE TABLE IF NOT EXISTS message_mentions (
    message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    mentioned_user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    CONSTRAINT pk_message_mentions PRIMARY KEY (message_id, mentioned_user_id)
);

-- Index for "find all messages where a user is mentioned" queries
CREATE INDEX IF NOT EXISTS idx_message_mentions_mentioned_user_id
    ON message_mentions(mentioned_user_id);

COMMENT ON TABLE message_mentions IS 'Mentioned users for messages (channel and conversation)';
