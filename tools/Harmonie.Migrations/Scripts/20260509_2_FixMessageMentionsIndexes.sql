-- Migration: Fix message_mentions indexes
-- Date: 2026-05-09
-- Description: Replace redundant idx_message_mentions_message_id (covered by PK)
--              with idx_message_mentions_mentioned_user_id for user-lookup queries.

DROP INDEX IF EXISTS idx_message_mentions_message_id;

CREATE INDEX IF NOT EXISTS idx_message_mentions_mentioned_user_id
    ON message_mentions(mentioned_user_id);
