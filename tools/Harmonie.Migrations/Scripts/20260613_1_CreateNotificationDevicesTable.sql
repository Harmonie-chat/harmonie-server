-- Migration: Create notification_devices table
-- Date: 2026-06-13
-- Description: Stores user notification delivery devices. Initial platform is Web Push for the PWA.

CREATE TABLE IF NOT EXISTS notification_devices (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    platform TEXT NOT NULL,
    token TEXT NOT NULL,
    web_push_p256dh TEXT NULL,
    web_push_auth TEXT NULL,
    expires_at_utc TIMESTAMPTZ NULL,
    created_at_utc TIMESTAMPTZ NOT NULL,
    updated_at_utc TIMESTAMPTZ NOT NULL,
    CONSTRAINT chk_notification_devices_token_not_empty
        CHECK (btrim(token) <> ''),
    CONSTRAINT chk_notification_devices_web_push_fields
        CHECK (
            platform <> 'web_push'
            OR (
                web_push_p256dh IS NOT NULL
                AND btrim(web_push_p256dh) <> ''
                AND web_push_auth IS NOT NULL
                AND btrim(web_push_auth) <> ''
            )
        )
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_notification_devices_platform_token
    ON notification_devices(platform, token);

CREATE INDEX IF NOT EXISTS idx_notification_devices_user_id
    ON notification_devices(user_id);

COMMENT ON TABLE notification_devices IS 'User notification delivery devices. Web Push is the first supported platform; FCM/APNs can be added later.';
COMMENT ON COLUMN notification_devices.platform IS 'Delivery platform, e.g. web_push, android_fcm, ios_apns.';
COMMENT ON COLUMN notification_devices.token IS 'Platform token. For Web Push this is the subscription endpoint.';
