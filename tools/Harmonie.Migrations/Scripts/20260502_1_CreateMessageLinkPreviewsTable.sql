CREATE TABLE message_link_previews (
    message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    url TEXT NOT NULL,
    title TEXT,
    description TEXT,
    image_url TEXT,
    site_name TEXT,
    fetched_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (message_id, url)
);

CREATE INDEX idx_link_previews_url_fetched ON message_link_previews(url, fetched_at_utc DESC);
