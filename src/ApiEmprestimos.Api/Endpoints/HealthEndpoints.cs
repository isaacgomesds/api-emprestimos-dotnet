using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiEmprestimos.Api.Endpoints;

public static class HealthEndpoints
{
    private static readonly int ServiceUnavailable = StatusCodes.Status503ServiceUnavailable;

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthResponseAsync,
            ResultStatusCodes = { [HealthStatus.Degraded] = ServiceUnavailable, [HealthStatus.Unhealthy] = ServiceUnavailable }
        })
        .WithName("HealthLive")
        .WithSummary("Check whether the application process is running");

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync,
            ResultStatusCodes = { [HealthStatus.Degraded] = ServiceUnavailable, [HealthStatus.Unhealthy] = ServiceUnavailable }
        })
        .WithName("HealthReady")
        .WithSummary("Check whether PostgreSQL and Redis are reachable");

        return endpoints;
    }

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds
            })
        });
    }
}
