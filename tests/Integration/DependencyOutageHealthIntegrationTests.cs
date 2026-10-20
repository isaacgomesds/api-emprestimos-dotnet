using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlOutageHealthTestCollection : ICollectionFixture<ApiIntegrationFixture>
{
    public const string Name = "API PostgreSQL outage integration tests";
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RedisOutageHealthTestCollection : ICollectionFixture<ApiIntegrationFixture>
{
    public const string Name = "API Redis outage integration tests";
}

[Collection(PostgreSqlOutageHealthTestCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PostgreSqlOutageHealthIntegrationTests(ApiIntegrationFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Readiness_returns_service_unavailable_when_postgresql_is_down()
    {
        await fixture.StopPostgreSqlAsync();

        using var response = await fixture.Client.GetAsync("/health/ready");

        await DependencyOutageHealthAssertions.AssertUnhealthyAsync(response, "postgresql");
    }
}

[Collection(RedisOutageHealthTestCollection.Name)]
[Trait("Category", "Integration")]
public sealed class RedisOutageHealthIntegrationTests(ApiIntegrationFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Readiness_returns_service_unavailable_when_redis_is_down()
    {
        await fixture.StopRedisAsync();

        using var response = await fixture.Client.GetAsync("/health/ready");

        await DependencyOutageHealthAssertions.AssertUnhealthyAsync(response, "redis");
    }
}

internal static class DependencyOutageHealthAssertions
{
    public static async Task AssertUnhealthyAsync(HttpResponseMessage response, string dependencyName)
    {
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("Unhealthy");
        json.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Should()
            .Contain(check => check.GetProperty("name").GetString() == dependencyName
                && check.GetProperty("status").GetString() == "Unhealthy");
    }
}
