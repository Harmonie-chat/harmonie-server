-- Add avatar appearance, theme, and language columns to users table.
-- Theme is NOT NULL with a default of 'default'; language is nullable.

ALTER TABLE users
    ADD COLUMN avatar_color VARCHAR(50),
    ADD COLUMN avatar_icon VARCHAR(50),
    ADD COLUMN avatar_bg VARCHAR(50),
    ADD COLUMN theme VARCHAR(50) NOT NULL DEFAULT 'default',
    ADD COLUMN language VARCHAR(10);

-- Backfill: existing rows already get theme = 'default' via the column default.
-- Explicit UPDATE ensures any rows inserted between ALTER and this statement are covered.
UPDATE users SET theme = 'default' WHERE theme = 'default';
