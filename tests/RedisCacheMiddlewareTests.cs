using System.Security.Cryptography;
using System.Text;
using ApiEmprestimos.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace ApiEmprestimos.Tests;

public sealed class RedisCacheMiddlewareTests
{
    [Fact]
    public async Task Loan_creation_replays_successful_response_and_rejects_key_reuse_with_different_body()
    {
        using var redis = CreateRedisConnection();
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)));
        var responseKey = $"api-emprestimos:idempotency:loans:{keyHash}";
        var lockKey = $"{responseKey}:lock";
        var invocationCount = 0;

        try
        {
            var middleware = CreateMiddleware(
                redis,
                context =>
                {
                    invocationCount++;
                    context.Response.StatusCode = StatusCodes.Status201Created;
                    context.Response.Headers.Location = "/loans/created-loan";
                    return context.Response.WriteAsync("{\"id\":\"created-loan\"}");
                });

            var firstResponse = await InvokeAsync(middleware, "POST", "/loans", idempotencyKey, "{\"bookId\":\"book-1\"}");
            var replayedResponse = await InvokeAsync(
                middleware,
                "POST",
                "/loans",
                idempotencyKey,
                "{\"bookId\":\"book-1\"}");

            Assert.Equal(1, invocationCount);
            Assert.Equal(StatusCodes.Status201Created, replayedResponse.StatusCode);
            Assert.Equal(firstResponse.Body, replayedResponse.Body);
            Assert.Equal(firstResponse.Location, replayedResponse.Location);

            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                InvokeAsync(middleware, "POST", "/loans", idempotencyKey, "{\"bookId\":\"book-2\"}"));
        }
        finally
        {
            await redis.GetDatabase().KeyDeleteAsync([responseKey, lockKey]);
        }
    }

    [Fact]
    public async Task Concurrent_loan_requests_with_the_same_key_execute_business_logic_once()
    {
        using var redis = CreateRedisConnection();
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)));
        var responseKey = $"api-emprestimos:idempotency:loans:{keyHash}";
        var lockKey = $"{responseKey}:lock";
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;

        try
        {
            var middleware = CreateMiddleware(
                redis,
                async context =>
                {
                    Interlocked.Increment(ref invocationCount);
                    requestStarted.TrySetResult();
                    await allowResponse.Task.WaitAsync(context.RequestAborted);
                    context.Response.StatusCode = StatusCodes.Status201Created;
                    await context.Response.WriteAsync("{\"id\":\"concurrent-loan\"}");
                });

            var firstRequest = InvokeAsync(
                middleware,
                "POST",
                "/loans",
                idempotencyKey,
                "{\"bookId\":\"book-1\"}");
            await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var secondRequest = InvokeAsync(
                middleware,
                "POST",
                "/loans",
                idempotencyKey,
                "{\"bookId\":\"book-1\"}");

            await Task.Delay(TimeSpan.FromMilliseconds(250));
            allowResponse.TrySetResult();
            var responses = await Task.WhenAll(firstRequest, secondRequest);

            Assert.Equal(1, invocationCount);
            Assert.Equal(responses[0].Body, responses[1].Body);
        }
        finally
        {
            allowResponse.TrySetResult();
            await redis.GetDatabase().KeyDeleteAsync([responseKey, lockKey]);
        }
    }

    [Fact]
    public async Task Catalog_cache_is_invalidated_by_book_and_loan_mutations()
    {
        using var redis = CreateRedisConnection();
        var bookId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        var invocationCount = 0;
        var idempotencyResponseKeys = new List<string>();
        var middleware = CreateMiddleware(
            redis,
            async context =>
            {
                invocationCount++;
                if (HttpMethods.IsGet(context.Request.Method))
                {
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsync($"catalog-{invocationCount}");
                    return;
                }

                context.Response.StatusCode = HttpMethods.IsPost(context.Request.Method)
                    && context.Request.Path.Value?.TrimEnd('/') == "/loans"
                        ? StatusCodes.Status201Created
                        : StatusCodes.Status204NoContent;
            });

        var mutations = new (string Method, string Path, string? IdempotencyKey)[]
        {
            ("POST", "/books", null),
            ("PATCH", $"/books/{bookId}", null),
            ("DELETE", $"/books/{bookId}", null),
            ("POST", "/loans", Guid.NewGuid().ToString("N")),
            ("POST", $"/loans/{loanId}/return", null),
            ("POST", $"/loans/{loanId}/cancel", null)
        };

        try
        {
            foreach (var mutation in mutations)
            {
                var query = $"?case={Guid.NewGuid():N}";
                var firstRead = await InvokeAsync(middleware, "GET", $"/books{query}", null, string.Empty);
                var repeatedRead = await InvokeAsync(middleware, "GET", $"/books{query}", null, string.Empty);
                Assert.Equal(firstRead.Body, repeatedRead.Body);

                var callsBeforeMutation = invocationCount;
                await InvokeAsync(
                    middleware,
                    mutation.Method,
                    mutation.Path,
                    mutation.IdempotencyKey,
                    "{}");
                Assert.Equal(callsBeforeMutation + 1, invocationCount);

                await InvokeAsync(middleware, "GET", $"/books{query}", null, string.Empty);
                Assert.Equal(callsBeforeMutation + 2, invocationCount);

                if (mutation.IdempotencyKey is not null)
                {
                    var keyHash = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(mutation.IdempotencyKey)));
                    idempotencyResponseKeys.Add($"api-emprestimos:idempotency:loans:{keyHash}");
                }
            }
        }
        finally
        {
            if (idempotencyResponseKeys.Count > 0)
            {
                await redis.GetDatabase().KeyDeleteAsync(
                    idempotencyResponseKeys.Select(key => (RedisKey)key).ToArray());
            }
        }
    }

    private static IConnectionMultiplexer CreateRedisConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable("Redis__ConnectionString") ?? "127.0.0.1:6379";
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    }

    private static RedisCacheMiddleware CreateMiddleware(
        IConnectionMultiplexer redis,
        RequestDelegate next)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:IdempotencyRetentionHours"] = "1",
                ["Redis:CatalogCacheSeconds"] = "300"
            })
            .Build();

        return new RedisCacheMiddleware(
            next,
            redis,
            configuration,
            NullLogger<RedisCacheMiddleware>.Instance);
    }

    private static async Task<CapturedResponse> InvokeAsync(
        RedisCacheMiddleware middleware,
        string method,
        string path,
        string? idempotencyKey,
        string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path.Split('?')[0];
        if (path.Contains('?'))
        {
            context.Request.QueryString = new QueryString(path[path.IndexOf('?')..]);
        }

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (idempotencyKey is not null)
        {
            context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        }

        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        await middleware.InvokeAsync(context);
        return new CapturedResponse(
            context.Response.StatusCode,
            context.Response.Headers.Location.ToString(),
            Encoding.UTF8.GetString(responseBody.ToArray()));
    }

    private sealed record CapturedResponse(int StatusCode, string Location, string Body);
}
