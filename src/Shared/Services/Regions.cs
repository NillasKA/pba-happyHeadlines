namespace HappyHeadlines.Shared;

public static class Regions
{
    public static string Normalize(string? region)
    {
        var normalized = region?.Trim().ToLowerInvariant();
        return normalized is "eu" or "na" ? normalized : "na";
    }
}
