-- Migration: Backfill duplicate channel names per guild
-- Date: 2026-03-01
-- Description: Ensures one unique channel name per guild before adding a unique index

WITH ranked AS (
    SELECT id,
           ROW_NUMBER() OVER (
               PARTITION BY guild_id, name
               ORDER BY created_at_utc ASC, id ASC
           ) AS row_number
    FROM guild_channels
),
duplicates AS (
    SELECT id
    FROM ranked
    WHERE row_number > 1
)
UPDATE guild_channels gc
SET name = LEFT(gc.name, 91) || '-' || RIGHT(gc.id::text, 8)
FROM duplicates d
WHERE gc.id = d.id;

