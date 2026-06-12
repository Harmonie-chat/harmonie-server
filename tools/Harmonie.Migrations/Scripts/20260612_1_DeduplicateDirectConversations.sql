-- Migration: Deduplicate direct conversations
-- Date: 2026-06-12
-- Description: A race in direct conversation creation could persist several
--              conversations for the same user pair: the lookup insert used
--              ON CONFLICT DO NOTHING, so the losing request silently kept its
--              own conversation (issue #401). This script merges every
--              duplicate into the canonical conversation referenced by
--              direct_conversation_lookup, then removes the duplicates.
--              Read states that exist on both sides are dropped with the
--              duplicate: worst case some messages show as unread again,
--              never as silently read.

-- Step 1: ensure every direct pair has a canonical lookup entry.
-- Normally guaranteed by the lookup table backfill; this is a fail-safe so the
-- merge below never deletes a conversation without a canonical target.
-- Oldest conversation of the pair wins.
INSERT INTO direct_conversation_lookup (user1_id, user2_id, conversation_id)
SELECT DISTINCT ON (cp1.user_id, cp2.user_id)
       cp1.user_id,
       cp2.user_id,
       c.id
FROM conversations c
JOIN conversation_participants cp1 ON cp1.conversation_id = c.id
JOIN conversation_participants cp2 ON cp2.conversation_id = c.id AND cp1.user_id < cp2.user_id
WHERE c.type = 'direct'
ORDER BY cp1.user_id, cp2.user_id, c.created_at_utc, c.id
ON CONFLICT (user1_id, user2_id) DO NOTHING;

-- Step 2: re-point messages from duplicate conversations to the canonical one.
UPDATE messages m
SET conversation_id = dcl.conversation_id
FROM conversations c
JOIN conversation_participants cp1 ON cp1.conversation_id = c.id
JOIN conversation_participants cp2 ON cp2.conversation_id = c.id AND cp1.user_id < cp2.user_id
JOIN direct_conversation_lookup dcl ON dcl.user1_id = cp1.user_id AND dcl.user2_id = cp2.user_id
WHERE m.conversation_id = c.id
  AND c.type = 'direct'
  AND c.id <> dcl.conversation_id;

-- Step 3: move read states to the canonical conversation when the user has
-- none there yet. Conflicting read states stay on the duplicate and are
-- removed with it in step 4.
UPDATE conversation_read_states crs
SET conversation_id = dcl.conversation_id
FROM conversations c
JOIN conversation_participants cp1 ON cp1.conversation_id = c.id
JOIN conversation_participants cp2 ON cp2.conversation_id = c.id AND cp1.user_id < cp2.user_id
JOIN direct_conversation_lookup dcl ON dcl.user1_id = cp1.user_id AND dcl.user2_id = cp2.user_id
WHERE crs.conversation_id = c.id
  AND c.type = 'direct'
  AND c.id <> dcl.conversation_id
  AND NOT EXISTS (
      SELECT 1
      FROM conversation_read_states existing
      WHERE existing.user_id = crs.user_id
        AND existing.conversation_id = dcl.conversation_id
  );

-- Step 4: delete duplicate conversations. Participants and leftover read
-- states are removed by ON DELETE CASCADE; messages were moved in step 2.
DELETE FROM conversations c
USING conversation_participants cp1,
      conversation_participants cp2,
      direct_conversation_lookup dcl
WHERE cp1.conversation_id = c.id
  AND cp2.conversation_id = c.id
  AND cp1.user_id < cp2.user_id
  AND dcl.user1_id = cp1.user_id
  AND dcl.user2_id = cp2.user_id
  AND c.type = 'direct'
  AND c.id <> dcl.conversation_id;
