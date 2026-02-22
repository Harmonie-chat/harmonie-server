-- Migration: Create channel_messages table
-- Date: 2026-02-22
-- Description: Persistent text messages stored per guild text channel

CREATE TABLE IF NOT EXISTS channel_messages (
    id UUID PRIMARY KEY,
    channel_id UUID NOT NULL REFERENCES guild_channels(id) ON DELETE CASCADE,
    author_user_id UUID NOT NULL REFERENCES users(id),
    content VARCHAR(4000) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_channel_messages_channel_created
    ON channel_messages(channel_id, created_at_utc DESC, id DESC);

COMMENT ON TABLE channel_messages IS 'Text messages posted in guild text channels';
