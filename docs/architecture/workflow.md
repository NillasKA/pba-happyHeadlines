# Project workflow

1. PublisherService validates an article and publishes `article.published` to RabbitMQ.
2. NewsletterService publishes `article.to-store` and, when the shared subscriber feature is enabled, stores subscriber work in its own database outbox. An outbox worker retries publication to `subscriber.notifications`.
3. ArticleService consumes the storage event, selects the EU or NA database and invalidates the corresponding cache entry. SubscriberService consumes the separate queue and records notification processing in its database; it does not send email. Subscriber records are deduplicated after a retry or duplicate delivery.
4. Readers use the gateway to read articles and comments. Comment writes call ProfanityService through a circuit breaker; unavailable moderation fails closed.

The feature state is shared in Redis and defaults to disabled when missing or unavailable. The disabled state blocks subscriber API operations and pauses queued work; already in-flight operations can finish. New work skipped while disabled is not backfilled. Malformed subscriber events are rejected without requeue; transient database errors are retried.

The separate prefill worker reads recent articles from both regional databases and populates Redis. A failure in one region is logged without preventing the other region from being processed; the next attempt occurs at the configured interval. ArticleService replicas do not each run a prefill loop.
