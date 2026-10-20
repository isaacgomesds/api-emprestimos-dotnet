using ApiEmprestimos.Application;
using ApiEmprestimos.Api.Endpoints;
using ApiEmprestimos.Api.Middleware;
using ApiEmprestimos.Infrastructure;
using ApiEmprestimos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
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

var app = builder.Build();

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

app.Run();
