using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ApiEmprestimos.Infrastructure.HealthChecks;

public static class DependencyHealthMetrics
{
    public const string MeterName = "ApiEmprestimos.Dependencies";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> CheckCounter =
        Meter.CreateCounter<long>("api.dependency.health.checks", "{check}");
    private static readonly Histogram<double> CheckDuration =
        Meter.CreateHistogram<double>("api.dependency.health.duration", "ms");

    public static void Record(string dependency, bool healthy, long startedTimestamp)
    {
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
        var tags = new TagList
        {
            { "dependency.name", dependency },
            { "dependency.status", healthy ? "healthy" : "unhealthy" }
        };

        CheckCounter.Add(1, tags);
        CheckDuration.Record(elapsedMilliseconds, tags);
    }
}
