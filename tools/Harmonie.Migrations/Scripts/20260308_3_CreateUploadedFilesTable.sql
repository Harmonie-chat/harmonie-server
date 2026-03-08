-- Migration: Create uploaded_files table
-- Date: 2026-03-08
-- Description: Stores metadata for files uploaded to object storage.

CREATE TABLE IF NOT EXISTS uploaded_files (
    id UUID PRIMARY KEY,
    uploader_id UUID NOT NULL REFERENCES users(id),
    filename VARCHAR(255) NOT NULL,
    content_type VARCHAR(255) NOT NULL,
    size_bytes BIGINT NOT NULL,
    storage_key VARCHAR(1024) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL,
    CONSTRAINT chk_uploaded_files_size_positive CHECK (size_bytes > 0)
);

CREATE INDEX IF NOT EXISTS ix_uploaded_files_uploader_id_created_at_utc
    ON uploaded_files(uploader_id, created_at_utc DESC, id DESC);

CREATE UNIQUE INDEX IF NOT EXISTS ux_uploaded_files_storage_key
    ON uploaded_files(storage_key);

COMMENT ON TABLE uploaded_files IS 'Metadata for files stored in object storage';
