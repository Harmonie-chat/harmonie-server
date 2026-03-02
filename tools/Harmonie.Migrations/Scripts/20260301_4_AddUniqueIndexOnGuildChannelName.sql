-- Migration: Enforce unique channel name within a guild
-- Date: 2026-03-01
-- Description: Prevents duplicate channel names per guild

CREATE UNIQUE INDEX IF NOT EXISTS ux_guild_channels_guild_id_name
    ON guild_channels(guild_id, name);

