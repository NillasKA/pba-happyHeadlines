namespace HappyHeadlines.Shared;

public static class CacheKeys
{
    public static string Article(Guid id, string region) => $"article:{Regions.Normalize(region)}:{id}";
    public static string Comments(Guid articleId, string region) => $"comments:{Regions.Normalize(region)}:{articleId}";
    public static string CommentLru(string region) => $"comments:lru:{Regions.Normalize(region)}:30";
}
