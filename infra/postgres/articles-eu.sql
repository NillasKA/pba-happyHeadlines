CREATE TABLE IF NOT EXISTS articles (
    region text NOT NULL CHECK (region = 'eu'),
    id uuid NOT NULL,
    title text NOT NULL,
    body text NOT NULL,
    published_at timestamptz NOT NULL,
    PRIMARY KEY (region, id)
);
CREATE INDEX IF NOT EXISTS articles_published_at_idx ON articles (region, published_at DESC);
