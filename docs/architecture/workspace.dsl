workspace "Happy Headlines" "Course project architecture" {
  model {
    publisher = person "Publisher" "Submits an article for publication."
    reader = person "Reader" "Reads articles and comments."

    system = softwareSystem "Happy Headlines" "A distributed article publishing and reading system." {
      gateway = container "API Gateway" "Routes client requests to services and regional lanes." "NGINX"
      publisherService = container "PublisherService" "Accepts publications and emits events." "ASP.NET Core" {
        publishApi = component "Publish API" "Validates publication requests." "Minimal API"
        eventPublisher = component "Event Publisher" "Publishes durable article events with trace context." "RabbitMQ.Client"
      }
      queue = container "ArticleQueue" "Durable asynchronous article event queue." "RabbitMQ"
      articleStoreQueue = container "ArticleStoreQueue" "Durable article persistence command queue." "RabbitMQ"
      newsletterService = container "NewsletterService" "Consumes article events and coordinates processing." "ASP.NET Core" {
        eventConsumer = component "Article Event Consumer" "Consumes and acknowledges article events." "RabbitMQ.Client"
        articleCoordinator = component "Article Coordinator" "Records the newsletter action." "ASP.NET Core"
        storePublisher = component "Article Store Publisher" "Republishes the article to the persistence queue." "RabbitMQ.Client"
      }
      articleService = container "ArticleService" "Owns articles and article read caching." "ASP.NET Core" {
        articleApi = component "Article API" "Serves article read requests." "Minimal API"
        articleConsumer = component "Article Persistence Consumer" "Consumes article storage commands." "RabbitMQ.Client"
        articleRepository = component "Article Repository" "Persists and preloads recent articles." "Npgsql"
        articleCache = component "Article Cache" "Read-through cache and scheduled prefill." "StackExchange.Redis"
      }
      prefillJob = container "Article Prefill Job" "Prefills recent articles into Redis every six hours." ".NET Worker"
      commentService = container "CommentService" "Owns comments and comment read caching." "ASP.NET Core" {
        commentApi = component "Comment API" "Serves comment reads and writes." "Minimal API"
        commentRepository = component "Comment Repository" "Persists comments." "Npgsql"
        commentCache = component "Comment Cache" "Read-on-miss cache with bounded recent-article index." "StackExchange.Redis"
      }
      articleDb = container "Article PostgreSQL" "Persists region-partitioned articles." "PostgreSQL"
      commentDb = container "Comment PostgreSQL" "Persists region-partitioned comments." "PostgreSQL"
      redis = container "Redis" "Shared region-aware article and comment cache." "Redis"

      publisherService -> queue "Publishes ArticlePublished with trace context" "AMQP"
      newsletterService -> queue "Consumes ArticlePublished" "AMQP"
      newsletterService -> articleStoreQueue "Publishes ArticleToStore" "AMQP"
      articleService -> articleStoreQueue "Consumes ArticleToStore" "AMQP"
      articleService -> articleDb "Reads/writes articles" "SQL"
      prefillJob -> articleDb "Reads articles published in the last 14 days" "SQL"
      prefillJob -> redis "Writes article cache keys with TTL" "Redis protocol"
      commentService -> commentDb "Reads/writes comments" "SQL"
      articleService -> redis "Reads/writes article cache" "Redis protocol"
      commentService -> redis "Reads/writes comment cache and recency index" "Redis protocol"

      publishApi -> eventPublisher "Validates and publishes" "in-process"
      eventPublisher -> queue "Publishes ArticlePublished with trace context" "AMQP"
      eventConsumer -> articleCoordinator "Handles article event" "in-process"
      articleCoordinator -> storePublisher "Requests article persistence" "in-process"
      storePublisher -> articleStoreQueue "Publishes ArticleToStore with trace context" "AMQP"
      articleConsumer -> articleRepository "Persists consumed article" "in-process"
      articleStoreQueue -> articleConsumer "Delivers ArticleToStore" "AMQP"
      articleApi -> articleRepository "Loads and stores articles" "in-process"
      articleApi -> articleCache "Reads and invalidates cache" "in-process"
      articleRepository -> articleDb "Reads/writes articles" "SQL"
      articleCache -> redis "Reads/writes articles and prefill" "Redis protocol"
      commentApi -> commentRepository "Loads and stores comments" "in-process"
      commentApi -> commentCache "Reads, writes, and invalidates cache" "in-process"
      commentRepository -> commentDb "Reads/writes comments" "SQL"
      commentCache -> redis "Reads/writes comments and LRU index" "Redis protocol"
    }

    prometheus = softwareSystem "Prometheus" "Scrapes service metrics."
    grafana = softwareSystem "Grafana" "Visualizes cache and service health."
    seq = softwareSystem "Seq" "Stores structured service logs."
    zipkin = softwareSystem "Zipkin" "Displays distributed traces."

    publisher -> gateway "Publishes and reads content" "HTTPS"
    reader -> gateway "Reads articles and comments" "HTTPS"
    gateway -> publisherService "Routes /publish" "HTTP"
    gateway -> articleService "Routes /articles" "HTTP"
    gateway -> commentService "Routes /articles/{id}/comments" "HTTP"
    prometheus -> publisherService "Scrapes /metrics" "HTTP"
    prometheus -> newsletterService "Scrapes /metrics" "HTTP"
    prometheus -> articleService "Scrapes /metrics" "HTTP"
    prometheus -> commentService "Scrapes /metrics" "HTTP"
    grafana -> prometheus "Queries metrics" "PromQL"
    publisherService -> seq "Writes structured logs" "HTTP"
    newsletterService -> seq "Writes structured logs" "HTTP"
    articleService -> seq "Writes structured logs" "HTTP"
    commentService -> seq "Writes structured logs" "HTTP"
    publisherService -> zipkin "Exports spans" "HTTP"
    newsletterService -> zipkin "Exports spans" "HTTP"
    articleService -> zipkin "Exports spans" "HTTP"
    commentService -> zipkin "Exports spans" "HTTP"
  }

  views {
    systemContext system "context" {
      include *
      autolayout lr
    }
    container system "containers" {
      include *
      autolayout lr
    }
    component publisherService "publisher-components" {
      include *
      autolayout lr
    }
    component newsletterService "newsletter-components" {
      include *
      autolayout lr
    }
    component articleService "article-components" {
      include *
      autolayout lr
    }
    component commentService "comment-components" {
      include *
      autolayout lr
    }
    styles {
      element "Person" {
        shape person
      }
      element "Software System" {
        background #1168bd
        color #ffffff
      }
      element "Container" {
        background #438dd5
        color #ffffff
      }
    }
    theme default
  }
}
