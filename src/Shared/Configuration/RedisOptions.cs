namespace HappyHeadlines.Shared;

public sealed class RedisOptions
{
    public string Connection { get; set; } = "localhost:6379,abortConnect=false";
}
