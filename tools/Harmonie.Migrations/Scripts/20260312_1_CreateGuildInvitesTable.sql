-- Migration: Create guild_invites table
-- Date: 2026-03-12
-- Description: Stores guild invite links with optional expiration and max uses.

CREATE TABLE guild_invites (
    id UUID PRIMARY KEY,
    code VARCHAR(8) NOT NULL UNIQUE,
    guild_id UUID NOT NULL REFERENCES guilds(id) ON DELETE CASCADE,
    creator_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    max_uses INT NULL,
    uses_count INT NOT NULL DEFAULT 0,
    expires_at_utc TIMESTAMPTZ NULL,
    created_at_utc TIMESTAMPTZ NOT NULL
);

CREATE INDEX idx_guild_invites_guild_id ON guild_invites(guild_id);
CREATE INDEX idx_guild_invites_code ON guild_invites(code);

COMMENT ON TABLE guild_invites IS 'Guild invite links with optional expiration and usage limits';
COMMENT ON COLUMN guild_invites.code IS 'Unique 8-char alphanumeric invite code';
COMMENT ON COLUMN guild_invites.max_uses IS 'Maximum number of uses. NULL = unlimited';
COMMENT ON COLUMN guild_invites.uses_count IS 'Current number of times this invite has been used';
COMMENT ON COLUMN guild_invites.expires_at_utc IS 'Expiration timestamp. NULL = never expires';
