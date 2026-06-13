-- Migration: Create message_notification_outbox table
-- Date: 2026-06-13
-- Description: Stores transport-agnostic message notification jobs for asynchronous worker dispatch.

CREATE TABLE IF NOT EXISTS message_notification_outbox (
    id UUID PRIMARY KEY,
    message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    status TEXT NOT NULL,
    attempts INTEGER NOT NULL DEFAULT 0,
    next_attempt_at_utc TIMESTAMPTZ NOT NULL,
    locked_until_utc TIMESTAMPTZ NULL,
    last_error TEXT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL,
    processed_at_utc TIMESTAMPTZ NULL,
    CONSTRAINT chk_message_notification_outbox_status
        CHECK (status IN ('pending', 'processing', 'processed', 'failed')),
    CONSTRAINT chk_message_notification_outbox_attempts_non_negative
        CHECK (attempts >= 0)
);

CREATE INDEX IF NOT EXISTS idx_message_notification_outbox_pending
    ON message_notification_outbox(status, next_attempt_at_utc, created_at_utc)
    WHERE status = 'pending';

CREATE INDEX IF NOT EXISTS idx_message_notification_outbox_processing_lock
    ON message_notification_outbox(status, locked_until_utc)
    WHERE status = 'processing';

CREATE INDEX IF NOT EXISTS idx_message_notification_outbox_message_id
    ON message_notification_outbox(message_id);

COMMENT ON TABLE message_notification_outbox IS 'Transport-agnostic outbox for asynchronous message notification dispatch';
COMMENT ON COLUMN message_notification_outbox.message_id IS 'Message whose notification recipients and platform payloads are resolved during worker dispatch';
