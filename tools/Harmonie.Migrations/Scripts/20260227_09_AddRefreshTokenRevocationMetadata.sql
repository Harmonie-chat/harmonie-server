-- Migration: Add refresh token revocation metadata
-- Date: 2026-02-27
-- Description: Add revocation reason and replacement linkage for refresh token reuse detection

ALTER TABLE refresh_tokens
    ADD COLUMN IF NOT EXISTS revocation_reason VARCHAR(64);

ALTER TABLE refresh_tokens
    ADD COLUMN IF NOT EXISTS replaced_by_token_id UUID;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_refresh_tokens_replaced_by_token_id') THEN
        ALTER TABLE refresh_tokens
            ADD CONSTRAINT fk_refresh_tokens_replaced_by_token_id
                FOREIGN KEY (replaced_by_token_id)
                REFERENCES refresh_tokens(id)
                ON DELETE SET NULL;
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS idx_refresh_tokens_replaced_by_token_id
    ON refresh_tokens(replaced_by_token_id);

COMMENT ON COLUMN refresh_tokens.revocation_reason
    IS 'Reason for revocation (e.g. rotated, logout, logout_all, reuse_detected)';

COMMENT ON COLUMN refresh_tokens.replaced_by_token_id
    IS 'Reference to the token that replaced this token during rotation';
