using HappyHeadlines.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("ProfanityService");
var app = builder.Build();
app.UseHappyHeadlinesMetrics("ProfanityService");
app.MapPost("/moderation/check", (ModerationRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Body)) return Results.BadRequest(new { error = "body is required" });
    var blocked = new[] { "damn", "shit", "fuck" };
    var words = System.Text.RegularExpressions.Regex.Matches(request.Body, @"\b[\p{L}]+\b")
        .Select(match => match.Value);
    return Results.Ok(new ModerationResult(!words.Any(word => blocked.Contains(word, StringComparer.OrdinalIgnoreCase))));
});
app.Run();
