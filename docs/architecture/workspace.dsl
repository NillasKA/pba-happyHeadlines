workspace "Happy Headlines" "Course project architecture" {
  model {
    publisher = person "Publisher" "Submits articles."
    reader = person "Reader" "Reads articles and posts comments."
    system = softwareSystem "Happy Headlines" "Article publishing and reading system." {
      gateway = container "API Gateway" "Routes requests and balances ArticleService replicas." "NGINX"
      publisherService = container "PublisherService" "Publishes article events." "ASP.NET Core"
      newsletterService = container "NewsletterService" "Forwards articles and stores enabled subscriber work for outbox delivery." "ASP.NET Core"
      articleService = container "ArticleService" "Serves and persists articles." "ASP.NET Core" {
        articleApi = component "ArticlesController" "Handles article read requests." "ASP.NET Core MVC"
        consumer = component "ArticlePersistenceConsumer" "Consumes article storage messages." "RabbitMQ.Client"
        articleLogic = component "ArticleService" "Coordinates article reads, cache and persistence." ".NET"
        repository = component "ArticleRepository" "Reads and writes regional articles." "Npgsql"
        shardRouter = component "ArticleShardRouter" "Selects a regional database connection." "Npgsql"
        cache = component "ArticleCache" "Caches individual article reads." "StackExchange.Redis"
      }
      prefill = container "Article Prefill Job" "Periodically preloads recent articles into Redis." ".NET Worker"
      commentService = container "CommentService" "Moderates and stores comments." "ASP.NET Core"
      profanityService = container "ProfanityService" "Checks comment text for prohibited language." "ASP.NET Core"
      subscriberService = container "SubscriberService" "Stores subscribers and processes notification events." "ASP.NET Core"
      broker = container "RabbitMQ" "Hosts article.published, article.to-store and subscriber.notifications queues." "RabbitMQ"
      articleEu = container "Article database EU" "Stores EU articles." "PostgreSQL"
      articleNa = container "Article database NA" "Stores NA articles." "PostgreSQL"
      commentDb = container "Comment database" "Stores comments." "PostgreSQL"
      newsletterDb = container "Newsletter database" "Stores pending subscriber notifications in an outbox." "PostgreSQL"
      subscriberDb = container "Subscriber database" "Stores subscribers and notification records." "PostgreSQL"
      redis = container "Redis" "Caches articles and comments." "Redis"
      gateway -> publisherService "Routes publication requests" "HTTP"
      gateway -> articleService "Balances article reads across replicas" "HTTP"
      gateway -> commentService "Routes comment requests" "HTTP"
      gateway -> subscriberService "Routes subscriber requests" "HTTP"
      gateway -> newsletterService "Routes feature flag requests" "HTTP"
      publisherService -> broker "Publishes article.published" "AMQP"
      newsletterService -> broker "Consumes article.published; publishes article.to-store and outbox notifications" "AMQP"
      articleService -> broker "Consumes article.to-store" "AMQP"
      subscriberService -> broker "Consumes subscriber.notifications" "AMQP"
      subscriberService -> subscriberDb "Reads and writes subscriber data" "SQL"
      newsletterService -> newsletterDb "Stores and reads pending subscriber notifications" "SQL"
      newsletterService -> redis "Reads and writes shared subscriber feature state" "Redis protocol"
      subscriberService -> redis "Reads shared subscriber feature state" "Redis protocol"
      articleService -> articleEu "Reads and writes EU articles" "SQL"
      articleService -> articleNa "Reads and writes NA articles" "SQL"
      prefill -> articleEu "Reads recent EU articles" "SQL"
      prefill -> articleNa "Reads recent NA articles" "SQL"
      prefill -> redis "Writes cached articles" "Redis protocol"
      articleService -> redis "Reads and writes cache entries" "Redis protocol"
      commentService -> redis "Reads and writes cache entries" "Redis protocol"
      commentService -> commentDb "Reads and writes comments" "SQL"
      commentService -> profanityService "Checks comment text through a circuit breaker" "HTTP"
      articleApi -> articleLogic "Requests article reads" "in-process"
      consumer -> articleLogic "Requests article persistence" "in-process"
      articleLogic -> repository "Reads and persists articles" "in-process"
      articleLogic -> cache "Reads, writes and invalidates cached articles" "in-process"
      repository -> shardRouter "Selects region connection" "in-process"
      shardRouter -> articleEu "Selects EU connection" "SQL"
      shardRouter -> articleNa "Selects NA connection" "SQL"
      cache -> redis "Reads and writes cache entries" "Redis protocol"
      consumer -> broker "Consumes article.to-store" "AMQP"
    }
    prometheus = softwareSystem "Prometheus" "Collects metrics."
    grafana = softwareSystem "Grafana" "Displays metrics."
    seq = softwareSystem "Seq" "Stores structured logs."
    zipkin = softwareSystem "Zipkin" "Displays distributed traces."
    publisher -> gateway "Submits articles" "HTTP"
    reader -> gateway "Reads articles and comments" "HTTP"
    prometheus -> publisherService "Scrapes metrics" "HTTP"
    prometheus -> newsletterService "Scrapes metrics" "HTTP"
    prometheus -> articleService "Scrapes metrics" "HTTP"
    prometheus -> commentService "Scrapes metrics" "HTTP"
    prometheus -> profanityService "Scrapes metrics" "HTTP"
    prometheus -> subscriberService "Scrapes metrics" "HTTP"
    grafana -> prometheus "Queries metrics" "PromQL"
    publisherService -> seq "Writes logs" "HTTP"
    newsletterService -> seq "Writes logs" "HTTP"
    articleService -> seq "Writes logs" "HTTP"
    commentService -> seq "Writes logs" "HTTP"
    profanityService -> seq "Writes logs" "HTTP"
    subscriberService -> seq "Writes logs" "HTTP"
    prefill -> seq "Writes logs" "HTTP"
    publisherService -> zipkin "Exports traces" "HTTP"
    newsletterService -> zipkin "Exports traces" "HTTP"
    articleService -> zipkin "Exports traces" "HTTP"
    commentService -> zipkin "Exports traces" "HTTP"
    profanityService -> zipkin "Exports traces" "HTTP"
    subscriberService -> zipkin "Exports traces" "HTTP"
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
    component articleService "article-components" {
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
