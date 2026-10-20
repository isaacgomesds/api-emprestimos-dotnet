using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ApiEmprestimos.Infrastructure.HealthChecks;

public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await connection.GetDatabase().PingAsync();
            DependencyHealthMetrics.Record("redis", true, startedTimestamp);
            return HealthCheckResult.Healthy("Redis is reachable.");
        }
        catch (Exception exception)
        {
            DependencyHealthMetrics.Record("redis", false, startedTimestamp);
            return HealthCheckResult.Unhealthy("Redis connectivity check failed.", exception);
        }
    }
}
