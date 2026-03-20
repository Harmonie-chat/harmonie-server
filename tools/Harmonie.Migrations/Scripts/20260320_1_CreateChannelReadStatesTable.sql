-- Migration: Create channel_read_states table
-- Date: 2026-03-20
-- Description: Stores per-user per-channel read position for acknowledge (ack) feature

CREATE TABLE IF NOT EXISTS channel_read_states (
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    channel_id UUID NOT NULL REFERENCES guild_channels(id) ON DELETE CASCADE,
    last_read_message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    read_at_utc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (user_id, channel_id)
);

CREATE INDEX IF NOT EXISTS idx_channel_read_states_channel
    ON channel_read_states(channel_id);

COMMENT ON TABLE channel_read_states IS 'Per-user per-channel read position for tracking unread messages';
