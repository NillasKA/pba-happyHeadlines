CREATE TABLE IF NOT EXISTS comments (
    region text NOT NULL CHECK (region IN ('eu', 'na')),
    id uuid NOT NULL,
    article_id uuid NOT NULL,
    body text NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (region, id)
) PARTITION BY LIST (region);

CREATE TABLE IF NOT EXISTS comments_eu PARTITION OF comments FOR VALUES IN ('eu');
CREATE TABLE IF NOT EXISTS comments_na PARTITION OF comments FOR VALUES IN ('na');
CREATE INDEX IF NOT EXISTS comments_article_idx ON comments (region, article_id, created_at);
