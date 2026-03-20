-- Migration: Create conversation_read_states table
-- Date: 2026-03-20
-- Description: Stores per-user per-conversation read position for acknowledge (ack) feature

CREATE TABLE IF NOT EXISTS conversation_read_states (
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    conversation_id UUID NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    last_read_message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    read_at_utc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (user_id, conversation_id)
);

CREATE INDEX IF NOT EXISTS idx_conversation_read_states_conversation
    ON conversation_read_states(conversation_id);

COMMENT ON TABLE conversation_read_states IS 'Per-user per-conversation read position for tracking unread messages';
