CREATE TABLE message_reactions (
    message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    user_id    UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    emoji      VARCHAR(64) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (message_id, user_id, emoji)
);

CREATE INDEX ix_message_reactions_message_id
    ON message_reactions (message_id);
