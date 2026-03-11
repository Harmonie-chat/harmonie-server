-- Migration: Link users and guilds to uploaded_files by technical ID for owned assets.
-- Purpose: Local uploaded avatars and guild icons become first-class references instead of raw URLs.
-- Backfill: Existing local `/api/files/{id}` values are converted into `*_file_id` and the raw URL column is cleared.

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS avatar_file_id UUID NULL;

ALTER TABLE guilds
    ADD COLUMN IF NOT EXISTS icon_file_id UUID NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_users_avatar_file_id_uploaded_files') THEN
        ALTER TABLE users
            ADD CONSTRAINT fk_users_avatar_file_id_uploaded_files
                FOREIGN KEY (avatar_file_id) REFERENCES uploaded_files(id) ON DELETE SET NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_guilds_icon_file_id_uploaded_files') THEN
        ALTER TABLE guilds
            ADD CONSTRAINT fk_guilds_icon_file_id_uploaded_files
                FOREIGN KEY (icon_file_id) REFERENCES uploaded_files(id) ON DELETE SET NULL;
    END IF;
END $$;

UPDATE users u
SET avatar_file_id = uf.id,
    avatar_url = NULL
FROM uploaded_files uf
WHERE u.avatar_url ~ '^/api/files/[0-9a-fA-F-]{36}$'
  AND uf.id = substring(u.avatar_url from '^/api/files/([0-9a-fA-F-]{36})$')::uuid;

UPDATE guilds g
SET icon_file_id = uf.id,
    icon_url = NULL
FROM uploaded_files uf
WHERE g.icon_url ~ '^/api/files/[0-9a-fA-F-]{36}$'
  AND uf.id = substring(g.icon_url from '^/api/files/([0-9a-fA-F-]{36})$')::uuid;

CREATE INDEX IF NOT EXISTS ix_users_avatar_file_id
    ON users(avatar_file_id)
    WHERE avatar_file_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_guilds_icon_file_id
    ON guilds(icon_file_id)
    WHERE icon_file_id IS NOT NULL;

COMMENT ON COLUMN users.avatar_file_id IS 'Owned uploaded file used as the local avatar source';
COMMENT ON COLUMN guilds.icon_file_id IS 'Owned uploaded file used as the local guild icon source';
