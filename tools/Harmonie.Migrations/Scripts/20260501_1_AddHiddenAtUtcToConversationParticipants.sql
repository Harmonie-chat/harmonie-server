-- Migration: Add hidden_at_utc to conversation_participants
-- Date: 2026-05-01
-- Description: Allows hiding a direct conversation without removing the participant row

ALTER TABLE conversation_participants
ADD COLUMN hidden_at_utc TIMESTAMPTZ NULL;
