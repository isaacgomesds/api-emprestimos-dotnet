using System.Diagnostics;
using ApiEmprestimos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiEmprestimos.Infrastructure.HealthChecks;

public sealed class PostgreSqlHealthCheck(LibraryDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            DependencyHealthMetrics.Record("postgresql", canConnect, startedTimestamp);
            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL is unreachable.");
        }
        catch (Exception exception)
        {
            DependencyHealthMetrics.Record("postgresql", false, startedTimestamp);
            return HealthCheckResult.Unhealthy("PostgreSQL connectivity check failed.", exception);
        }
    }
}
