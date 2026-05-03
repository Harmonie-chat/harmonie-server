CREATE TABLE IF NOT EXISTS pinned_messages (
    message_id        UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    pinned_by_user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    pinned_at_utc     TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (message_id)
);

CREATE INDEX IF NOT EXISTS idx_pinned_messages_pinned_by_user
    ON pinned_messages (pinned_by_user_id);
