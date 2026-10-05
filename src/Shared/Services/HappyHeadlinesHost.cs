using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace HappyHeadlines.Shared;

public static class HappyHeadlinesHost
{
    public static void AddObservability(this WebApplicationBuilder builder, string serviceName)
    {
        var seqUrl = builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341";
        var zipkinEndpoint = builder.Configuration["Zipkin:Endpoint"] ?? "http://localhost:9411/api/v2/spans";

        builder.Host.UseSerilog((context, _, logger) => logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(new JsonFormatter())
            .WriteTo.Seq(seqUrl));

#pragma warning disable CS0618 // The course explicitly uses Zipkin for tracing demonstrations.
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddSource(TraceContext.SourceName)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/metrics"))
                .AddHttpClientInstrumentation()
                .AddZipkinExporter(options => options.Endpoint = new Uri(zipkinEndpoint)));
#pragma warning restore CS0618
    }

    public static void UseHappyHeadlinesMetrics(this WebApplication app, string serviceName)
    {
        app.UseHttpMetrics();
        app.Use(async (context, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            await next();
            var route = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.RouteNameMetadata>()?.RouteName
                        ?? context.GetEndpoint()?.DisplayName
                        ?? "unmatched";
            if (route.Length > 100)
                route = route[..100];
            var region = Regions.Normalize(context.Request.Headers["X-Region"].FirstOrDefault()
                                           ?? context.Request.Query["region"].FirstOrDefault());

            HappyMetrics.HttpRequests.WithLabels(serviceName, region, context.Request.Method, route,
                context.Response.StatusCode.ToString()).Inc();
            HappyMetrics.HttpDuration.WithLabels(serviceName, region, context.Request.Method, route)
                .Observe(Stopwatch.GetElapsedTime(started).TotalSeconds);
        });
        app.MapMetrics();
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = serviceName }));
    }
}
