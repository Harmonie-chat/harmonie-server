-- Migration: Drop legacy user1_id/user2_id columns from conversations
-- Date: 2026-03-26
-- Description: Removes the hardcoded 2-participant columns now superseded by
--              conversation_participants and direct_conversation_lookup.
-- WARNING: DESTRUCTIVE — run only after the application code is deployed and
--          verified to no longer reference these columns.

ALTER TABLE conversations
    DROP CONSTRAINT IF EXISTS chk_conversations_distinct_users;

DROP INDEX IF EXISTS ux_conversations_user_pair;

ALTER TABLE conversations
    DROP COLUMN user1_id,
    DROP COLUMN user2_id;
