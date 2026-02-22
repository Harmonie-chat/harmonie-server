-- Migration: Optimize guild channel list query index
-- Date: 2026-02-22
-- Description: Add index aligned with guild channel listing sort order

CREATE INDEX IF NOT EXISTS idx_guild_channels_guild_position_created_id
    ON guild_channels(guild_id, position ASC, created_at_utc ASC, id ASC);
