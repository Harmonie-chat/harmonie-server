-- Migration: Add updated_at_utc to channel_messages
-- Date: 2026-03-01
-- Description: Tracks when a message was last edited; NULL means the message has never been edited

ALTER TABLE channel_messages
    ADD COLUMN IF NOT EXISTS updated_at_utc TIMESTAMPTZ NULL;

COMMENT ON COLUMN channel_messages.updated_at_utc IS 'Timestamp of the last edit; NULL means the message has never been edited';
