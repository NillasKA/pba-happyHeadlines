using Npgsql;
using Microsoft.Extensions.Configuration;
namespace HappyHeadlines.Shared;
public sealed class ArticleShardRouter : IDisposable
{
    private readonly NpgsqlDataSource eu;
    private readonly NpgsqlDataSource na;
    public ArticleShardRouter(IConfiguration configuration)
    {
        var legacy = configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=articles;Username=happy;Password=happy";
        eu = NpgsqlDataSource.Create(configuration.GetConnectionString("ArticlesEu") ?? legacy);
        na = NpgsqlDataSource.Create(configuration.GetConnectionString("ArticlesNa") ?? legacy);
    }
    public NpgsqlDataSource ForRegion(string? region) => Regions.Normalize(region) == "eu" ? eu : na;
    public void Dispose() { eu.Dispose(); na.Dispose(); }
}
