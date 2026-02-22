-- Migration: Create guild_channels table
-- Date: 2026-02-22
-- Description: Guild channel metadata (text + voice placeholders)

CREATE TABLE IF NOT EXISTS guild_channels (
    id UUID PRIMARY KEY,
    guild_id UUID NOT NULL REFERENCES guilds(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    type SMALLINT NOT NULL,
    is_default BOOLEAN NOT NULL,
    position INT NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_guild_channels_guild_id ON guild_channels(guild_id);
CREATE INDEX IF NOT EXISTS idx_guild_channels_guild_id_type_position ON guild_channels(guild_id, type, position);

CREATE UNIQUE INDEX IF NOT EXISTS ux_guild_channels_default_text
    ON guild_channels(guild_id)
    WHERE type = 1 AND is_default = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS ux_guild_channels_default_voice
    ON guild_channels(guild_id)
    WHERE type = 2 AND is_default = TRUE;

COMMENT ON TABLE guild_channels IS 'Channels belonging to a guild; includes text and future voice placeholders';
