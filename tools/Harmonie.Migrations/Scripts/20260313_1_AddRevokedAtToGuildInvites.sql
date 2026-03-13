-- Migration: Add revoked_at_utc to guild_invites
-- Date: 2026-03-13
-- Description: Adds soft-delete support for guild invite revocation.

ALTER TABLE guild_invites
    ADD COLUMN revoked_at_utc TIMESTAMPTZ NULL;

COMMENT ON COLUMN guild_invites.revoked_at_utc IS 'Soft-delete timestamp. NULL = active. Set when an admin or creator revokes the invite.';
