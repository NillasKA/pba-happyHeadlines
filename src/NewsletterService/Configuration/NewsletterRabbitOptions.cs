namespace HappyHeadlines.NewsletterService.Configuration;

public sealed class NewsletterRabbitOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string InputQueue { get; set; } = "article.published";
    public string OutputQueue { get; set; } = "article.to-store";
}
