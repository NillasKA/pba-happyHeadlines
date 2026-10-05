using System.Text.Json;

namespace HappyHeadlines.Shared;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
