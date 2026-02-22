-- Migration: Create guilds table
-- Date: 2026-02-22
-- Description: Core guild aggregate storage

CREATE TABLE IF NOT EXISTS guilds (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    owner_user_id UUID NOT NULL REFERENCES users(id),
    created_at_utc TIMESTAMPTZ NOT NULL,
    updated_at_utc TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_guilds_owner_user_id ON guilds(owner_user_id);

COMMENT ON TABLE guilds IS 'Guild containers for collaborative channels and memberships';
COMMENT ON COLUMN guilds.owner_user_id IS 'Creator user identifier; creator is also Admin member';
