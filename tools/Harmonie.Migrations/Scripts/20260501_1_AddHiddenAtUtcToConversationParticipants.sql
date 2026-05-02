-- Migration: Add hidden_at_utc to conversation_participants
-- Date: 2026-05-01
-- Description: Allows hiding a direct conversation without removing the participant row

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'conversation_participants'
          AND column_name = 'hidden_at_utc'
    ) THEN
        ALTER TABLE conversation_participants
        ADD COLUMN hidden_at_utc TIMESTAMPTZ NULL;
    END IF;
END $$;
