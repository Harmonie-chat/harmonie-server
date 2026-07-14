-- Migration: Add indexes for notification cleanup worker
-- Date: 2026-07-14
-- Description: Supports batched retention cleanup of terminal outbox jobs and expired devices.
-- These indexes are built concurrently; the migration runner is explicitly configured without transactions.

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_message_notification_outbox_terminal_cleanup
    ON message_notification_outbox(status, processed_at_utc, id)
    WHERE status IN ('processed', 'failed')
      AND processed_at_utc IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_notification_devices_expiration_cleanup
    ON notification_devices(expires_at_utc, id)
    WHERE expires_at_utc IS NOT NULL;
