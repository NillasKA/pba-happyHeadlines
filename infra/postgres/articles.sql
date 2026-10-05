CREATE TABLE IF NOT EXISTS articles (
    region text NOT NULL CHECK (region IN ('eu', 'na')),
    id uuid NOT NULL,
    title text NOT NULL,
    body text NOT NULL,
    published_at timestamptz NOT NULL,
    PRIMARY KEY (region, id)
) PARTITION BY LIST (region);

CREATE TABLE IF NOT EXISTS articles_eu PARTITION OF articles FOR VALUES IN ('eu');
CREATE TABLE IF NOT EXISTS articles_na PARTITION OF articles FOR VALUES IN ('na');
CREATE INDEX IF NOT EXISTS articles_published_at_idx ON articles (region, published_at DESC);
