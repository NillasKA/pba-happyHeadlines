using System.Diagnostics;

namespace HappyHeadlines.Shared;

public static class TraceContext
{
    public const string SourceName = "HappyHeadlines";
    public static readonly ActivitySource Source = new(SourceName);

    public static IDictionary<string, object> Inject(Activity? activity)
    {
        var headers = new Dictionary<string, object>();
        if (activity?.Id is not null)
            headers["traceparent"] = activity.Id;
        if (!string.IsNullOrWhiteSpace(activity?.TraceStateString))
            headers["tracestate"] = activity.TraceStateString;
        return headers;
    }

    public static ActivityContext Extract(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue("traceparent", out var traceparent))
            return default;

        var parent = HeaderValue(traceparent);
        var state = headers.TryGetValue("tracestate", out var tracestate) ? HeaderValue(tracestate) : null;
        return ActivityContext.TryParse(parent, state, out var context) ? context : default;
    }

    private static string HeaderValue(object value) => value switch
    {
        byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
        ReadOnlyMemory<byte> memory => System.Text.Encoding.UTF8.GetString(memory.Span),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
    };
}
