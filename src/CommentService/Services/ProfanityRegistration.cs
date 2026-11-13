namespace HappyHeadlines.CommentService.Services;

public static class ProfanityRegistration
{
    public static IServiceCollection AddProfanityModeration(this IServiceCollection services, Uri baseAddress)
    {
        services.AddHttpClient("profanity", client => client.BaseAddress = baseAddress)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
        services.AddSingleton<IProfanityClient>(provider => new ProfanityClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("profanity"),
            provider.GetRequiredService<ILogger<ProfanityClient>>()));
        return services;
    }
}
