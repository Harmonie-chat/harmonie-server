-- Migration: Drop legacy message tables
-- Date: 2026-03-11
-- Description: Removes superseded channel_messages and direct_messages tables after the unified backfill is complete

DROP TABLE IF EXISTS channel_messages;
DROP TABLE IF EXISTS direct_messages;

DROP FUNCTION IF EXISTS set_channel_message_search_vector();
DROP FUNCTION IF EXISTS set_direct_message_search_vector();
