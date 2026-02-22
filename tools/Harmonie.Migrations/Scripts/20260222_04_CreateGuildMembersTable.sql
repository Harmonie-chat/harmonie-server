-- Migration: Create guild_members table
-- Date: 2026-02-22
-- Description: Guild membership and role assignments

CREATE TABLE IF NOT EXISTS guild_members (
    guild_id UUID NOT NULL REFERENCES guilds(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role SMALLINT NOT NULL,
    joined_at_utc TIMESTAMPTZ NOT NULL,
    invited_by_user_id UUID NULL REFERENCES users(id),
    PRIMARY KEY (guild_id, user_id)
);

CREATE INDEX IF NOT EXISTS idx_guild_members_user_id ON guild_members(user_id);
CREATE INDEX IF NOT EXISTS idx_guild_members_guild_id_role ON guild_members(guild_id, role);

COMMENT ON TABLE guild_members IS 'User memberships in guilds with role-based permissions';
