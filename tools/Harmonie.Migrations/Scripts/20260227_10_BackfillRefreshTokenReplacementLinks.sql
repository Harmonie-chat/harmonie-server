-- Migration: Backfill refresh token replacement links
-- Date: 2026-02-27
-- Description: Reconstruct replaced_by_token_id for historical rotated rows created before linkage existed

WITH candidate_links AS (
    SELECT
        old_token.id AS old_token_id,
        new_token.id AS new_token_id,
        ROW_NUMBER() OVER (
            PARTITION BY old_token.id
            ORDER BY new_token.created_at_utc, new_token.id
        ) AS candidate_rank
    FROM refresh_tokens old_token
    INNER JOIN refresh_tokens new_token
        ON new_token.user_id = old_token.user_id
       AND new_token.id <> old_token.id
       AND new_token.created_at_utc = old_token.revoked_at_utc
    WHERE old_token.replaced_by_token_id IS NULL
      AND old_token.revoked_at_utc IS NOT NULL
      AND old_token.created_at_utc < new_token.created_at_utc
      AND (old_token.revocation_reason IS NULL OR old_token.revocation_reason = 'rotated')
)
UPDATE refresh_tokens target
SET replaced_by_token_id = candidate_links.new_token_id
FROM candidate_links
WHERE candidate_links.candidate_rank = 1
  AND target.id = candidate_links.old_token_id
  AND target.replaced_by_token_id IS NULL;
