using ApiEmprestimos.Application;
using ApiEmprestimos.Api.Endpoints;
using ApiEmprestimos.Api.Middleware;
using ApiEmprestimos.Infrastructure;
using ApiEmprestimos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using StackExchange.Redis;
using ApiEmprestimos.Infrastructure.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "127.0.0.1:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
    redisOptions.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(redisOptions);
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApplication();
var connectionString = builder.Configuration.GetConnectionString("LibraryDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'LibraryDatabase' is not configured. Set ConnectionStrings__LibraryDatabase or configure it in appsettings.");
}

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddInfrastructure();

var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "ApiEmprestimos.Api";
var otlpEndpoint = builder.Configuration["OpenTelemetry:Otlp:Endpoint"]
    ?? builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddRedisInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(DependencyHealthMetrics.MeterName)
        .AddPrometheusExporter());

if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    openTelemetry.WithTracing(tracing => tracing.AddOtlpExporter());
    openTelemetry.WithMetrics(metrics => metrics.AddOtlpExporter());
}

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<RedisCacheMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapBookEndpoints();
app.MapUserEndpoints();
app.MapLoanEndpoints();
app.MapHealthEndpoints();
app.MapPrometheusScrapingEndpoint().DisableHttpMetrics();

app.Run();

public partial class Program
{
}
