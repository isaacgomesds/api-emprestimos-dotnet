using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class HealthAndTelemetryIntegrationTests(ApiIntegrationFixture fixture)
    : ApiIntegrationTestBase(fixture)
{
    [Fact]
    public async Task Liveness_is_healthy_and_readiness_reports_both_dependencies()
    {
        using var liveResponse = await Client.GetAsync("/health/live");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        liveResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        using var liveJson = JsonDocument.Parse(await liveResponse.Content.ReadAsStringAsync());
        liveJson.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

        using var readyResponse = await Client.GetAsync("/health/ready");
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var readyJson = JsonDocument.Parse(await readyResponse.Content.ReadAsStringAsync());
        readyJson.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        readyJson.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .Should()
            .Contain(["postgresql", "redis"]);
    }

    [Fact]
    public async Task Prometheus_endpoint_exports_http_runtime_and_dependency_health_metrics()
    {
        using var healthResponse = await Client.GetAsync("/health/ready");
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var catalogResponse = await Client.GetAsync("/books");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var metricsResponse = await Client.GetAsync("/metrics");
        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        metricsResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        var metrics = await metricsResponse.Content.ReadAsStringAsync();
        metrics.Should().Contain("http_server_request_duration");
        metrics.Should().Contain("dotnet_gc_collections_total");
        metrics.Should().Contain("api_dependency_health_checks");
        metrics.Should().Contain("dependency_name=\"postgresql\"");
        metrics.Should().Contain("dependency_name=\"redis\"");
    }
}
