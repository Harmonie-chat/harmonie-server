-- Migration: Backfill unified messages table
-- Date: 2026-03-11
-- Description: Copies existing channel and direct messages into the new messages table before the legacy tables are removed

DO $$
BEGIN
    IF to_regclass('public.channel_messages') IS NOT NULL THEN
        INSERT INTO messages (
            id,
            channel_id,
            conversation_id,
            author_user_id,
            content,
            created_at_utc,
            updated_at_utc,
            deleted_at_utc
        )
        SELECT cm.id,
               cm.channel_id,
               NULL,
               cm.author_user_id,
               cm.content,
               cm.created_at_utc,
               cm.updated_at_utc,
               cm.deleted_at_utc
        FROM channel_messages cm
        WHERE NOT EXISTS (
            SELECT 1
            FROM messages m
            WHERE m.id = cm.id
        );
    END IF;

    IF to_regclass('public.direct_messages') IS NOT NULL THEN
        INSERT INTO messages (
            id,
            channel_id,
            conversation_id,
            author_user_id,
            content,
            created_at_utc,
            updated_at_utc,
            deleted_at_utc
        )
        SELECT dm.id,
               NULL,
               dm.conversation_id,
               dm.author_user_id,
               dm.content,
               dm.created_at_utc,
               dm.updated_at_utc,
               dm.deleted_at_utc
        FROM direct_messages dm
        WHERE NOT EXISTS (
            SELECT 1
            FROM messages m
            WHERE m.id = dm.id
        );
    END IF;
END $$;
