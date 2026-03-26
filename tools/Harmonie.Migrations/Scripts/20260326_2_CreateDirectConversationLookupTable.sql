-- Migration: Create direct_conversation_lookup table
-- Date: 2026-03-26
-- Description: Provides a normalized lookup table for DM deduplication,
--              replacing the LEAST/GREATEST expression index on conversations.
--              Convention: user1_id = LEAST(a, b), user2_id = GREATEST(a, b).
--              Backfills from existing DM conversations.

CREATE TABLE direct_conversation_lookup (
    user1_id        UUID NOT NULL REFERENCES users(id),
    user2_id        UUID NOT NULL REFERENCES users(id),
    conversation_id UUID NOT NULL REFERENCES conversations(id),
    PRIMARY KEY (user1_id, user2_id)
);

-- Backfill existing DMs
INSERT INTO direct_conversation_lookup (user1_id, user2_id, conversation_id)
SELECT LEAST(user1_id, user2_id), GREATEST(user1_id, user2_id), id
FROM conversations;
