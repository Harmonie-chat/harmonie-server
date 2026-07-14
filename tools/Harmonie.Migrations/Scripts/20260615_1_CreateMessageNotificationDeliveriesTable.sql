-- Migration: Create message_notification_deliveries table
-- Date: 2026-06-15
-- Description: Tracks message notification delivery state per device so retries do not re-send to devices that already succeeded.
-- Backfill strategy: existing pending/processing outbox rows create delivery rows lazily on their next dispatch attempt from currently active devices. Historical processed/failed rows are terminal and do not need per-device delivery rows.

CREATE TABLE IF NOT EXISTS message_notification_deliveries (
    id UUID PRIMARY KEY,
    outbox_job_id UUID NOT NULL REFERENCES message_notification_outbox(id) ON DELETE CASCADE,
    device_id UUID NOT NULL REFERENCES notification_devices(id) ON DELETE CASCADE,
    status TEXT NOT NULL,
    attempts INTEGER NOT NULL DEFAULT 0,
    last_error TEXT NULL,
    first_attempted_at_utc TIMESTAMPTZ NULL,
    last_attempted_at_utc TIMESTAMPTZ NULL,
    succeeded_at_utc TIMESTAMPTZ NULL,
    created_at_utc TIMESTAMPTZ NOT NULL,
    updated_at_utc TIMESTAMPTZ NOT NULL,
    CONSTRAINT chk_message_notification_deliveries_attempts_non_negative
        CHECK (attempts >= 0),
    CONSTRAINT chk_message_notification_deliveries_status
        CHECK (status IN ('pending', 'succeeded', 'transient_failure', 'invalid_device', 'failed')),
    CONSTRAINT chk_message_notification_deliveries_succeeded_at
        CHECK (
            (status = 'succeeded' AND succeeded_at_utc IS NOT NULL)
            OR (status <> 'succeeded')
        )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_message_notification_deliveries_job_device
    ON message_notification_deliveries(outbox_job_id, device_id);

CREATE INDEX IF NOT EXISTS idx_message_notification_deliveries_job_status
    ON message_notification_deliveries(outbox_job_id, status);

CREATE INDEX IF NOT EXISTS idx_message_notification_deliveries_device_id
    ON message_notification_deliveries(device_id);

COMMENT ON TABLE message_notification_deliveries IS 'Tracks per-device delivery state for message notification outbox jobs.';
COMMENT ON COLUMN message_notification_deliveries.status IS 'Delivery status: pending, succeeded, transient_failure, invalid_device, failed.';
