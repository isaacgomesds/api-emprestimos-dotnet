using ApiEmprestimos.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ApiEmprestimos.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiIntegrationTestCollection : ICollectionFixture<ApiIntegrationFixture>
{
    public const string Name = "API integration tests";
}

public sealed class ApiIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("api_emprestimos_test")
        .WithUsername("postgres")
        .WithPassword("postgres-test-password")
        .Build();
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();
    private ApiWebApplicationFactory? _factory;
    private string? _originalPostgresConnectionString;
    private string? _originalRedisConnectionString;

    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _factory?.Services
        ?? throw new InvalidOperationException("The API host has not been initialized.");

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _originalPostgresConnectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__LibraryDatabase");
        _originalRedisConnectionString = Environment.GetEnvironmentVariable("Redis__ConnectionString");
        Environment.SetEnvironmentVariable("ConnectionStrings__LibraryDatabase", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Redis__ConnectionString", _redis.GetConnectionString());

        _factory = new ApiWebApplicationFactory(
            _postgres.GetConnectionString(),
            _redis.GetConnectionString());
        Client = _factory.CreateClient();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetStateAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE "AuditEvents", "Loans", "Books", "Users", "IdempotentRequests"
            RESTART IDENTITY CASCADE
            """);

        var redis = Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
        await redis.GetDatabase().ExecuteAsync("FLUSHDB");
    }

    public Task StopPostgreSqlAsync() => _postgres.StopAsync();

    public Task StartPostgreSqlAsync() => _postgres.StartAsync();

    public Task StopRedisAsync() => _redis.StopAsync();

    public Task StartRedisAsync() => _redis.StartAsync();

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__LibraryDatabase",
            _originalPostgresConnectionString);
        Environment.SetEnvironmentVariable("Redis__ConnectionString", _originalRedisConnectionString);
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }

    private sealed class ApiWebApplicationFactory(
        string postgresConnectionString,
        string redisConnectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LibraryDatabase"] = postgresConnectionString,
                    ["Redis:ConnectionString"] = redisConnectionString,
                    ["Redis:IdempotencyRetentionHours"] = "1",
                    ["Redis:CatalogCacheSeconds"] = "300"
                }));
        }
    }
}
