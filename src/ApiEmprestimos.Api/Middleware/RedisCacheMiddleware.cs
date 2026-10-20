using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace ApiEmprestimos.Api.Middleware;

public sealed class RedisCacheMiddleware(
    RequestDelegate next,
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<RedisCacheMiddleware> logger)
{
    private const string KeyPrefix = "api-emprestimos";
    private const string CatalogVersionKey = $"{KeyPrefix}:catalog:version";
    private static readonly TimeSpan LockExpiration = TimeSpan.FromHours(1);
    private static readonly TimeSpan LockWaitInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan LockWaitLimit = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string ReleaseLockScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly TimeSpan _idempotencyRetention = TimeSpan.FromHours(
        configuration.GetValue("Redis:IdempotencyRetentionHours", 24));
    private readonly TimeSpan _catalogCacheDuration = TimeSpan.FromSeconds(
        configuration.GetValue("Redis:CatalogCacheSeconds", 300));

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsBookCatalogRead(context.Request, out var catalogCacheKey))
        {
            await ServeCachedCatalogAsync(context, catalogCacheKey);
            return;
        }

        if (IsLoanCreation(context.Request))
        {
            await HandleIdempotentLoanCreationAsync(context);
            return;
        }

        await next(context);
        if (IsCatalogMutation(context.Request) && IsSuccessful(context.Response.StatusCode))
        {
            await _database.StringIncrementAsync(CatalogVersionKey);
        }
    }

    private async Task ServeCachedCatalogAsync(HttpContext context, string cacheKey)
    {
        var version = await _database.StringGetAsync(CatalogVersionKey);
        if (version.IsNull)
        {
            version = "0";
        }

        var versionedCacheKey = $"{cacheKey}:{version}";
        var cachedValue = await _database.StringGetAsync(versionedCacheKey);
        if (!cachedValue.IsNull)
        {
            var cachedResponse = JsonSerializer.Deserialize<CachedResponse>(cachedValue.ToString(), JsonOptions)
                ?? throw new InvalidOperationException("The cached catalog response is invalid.");
            await WriteResponseAsync(context, cachedResponse);
            return;
        }

        var response = await CaptureResponseAsync(context);
        if (response.StatusCode == StatusCodes.Status200OK)
        {
            await _database.StringSetAsync(
                versionedCacheKey,
                JsonSerializer.Serialize(response, JsonOptions),
                _catalogCacheDuration);
        }

        await WriteResponseAsync(context, response);
    }

    private async Task HandleIdempotentLoanCreationAsync(HttpContext context)
    {
        var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
        {
            await next(context);
            return;
        }

        var requestHash = await GetRequestHashAsync(context);
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)));
        var responseKey = $"{KeyPrefix}:idempotency:loans:{keyHash}";
        var lockKey = $"{responseKey}:lock";
        var lockToken = Guid.NewGuid().ToString("N");

        var cachedResponse = await GetCachedIdempotentResponseAsync(responseKey, requestHash);
        if (cachedResponse is not null)
        {
            await WriteResponseAsync(context, cachedResponse);
            return;
        }

        var lockAcquired = await _database.StringSetAsync(
            lockKey,
            lockToken,
            LockExpiration,
            When.NotExists);

        if (!lockAcquired)
        {
            var lockWaitDeadline = DateTime.UtcNow + LockWaitLimit;
            while (DateTime.UtcNow < lockWaitDeadline)
            {
                await Task.Delay(LockWaitInterval, context.RequestAborted);
                cachedResponse = await GetCachedIdempotentResponseAsync(responseKey, requestHash);
                if (cachedResponse is not null)
                {
                    await WriteResponseAsync(context, cachedResponse);
                    return;
                }

                lockAcquired = await _database.StringSetAsync(
                    lockKey,
                    lockToken,
                    LockExpiration,
                    When.NotExists);
                if (lockAcquired)
                {
                    break;
                }
            }
        }

        if (!lockAcquired)
        {
            logger.LogWarning("Idempotency key is still being processed.");
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Request in progress",
                    Detail = "A request with this Idempotency-Key is still being processed.",
                    Instance = context.Request.Path
                },
                context.RequestAborted);
            return;
        }

        try
        {
            cachedResponse = await GetCachedIdempotentResponseAsync(responseKey, requestHash);
            if (cachedResponse is not null)
            {
                await WriteResponseAsync(context, cachedResponse);
                return;
            }

            var response = await CaptureResponseAsync(context);
            if (IsSuccessful(response.StatusCode))
            {
                await _database.StringIncrementAsync(CatalogVersionKey);
                response = response with { RequestHash = requestHash };
                await _database.StringSetAsync(
                    responseKey,
                    JsonSerializer.Serialize(response, JsonOptions),
                    _idempotencyRetention);
            }

            await WriteResponseAsync(context, response);
        }
        finally
        {
            await _database.ScriptEvaluateAsync(
                ReleaseLockScript,
                [lockKey],
                [lockToken]);
        }
    }

    private async Task<CachedResponse?> GetCachedIdempotentResponseAsync(
        string responseKey,
        string requestHash)
    {
        var cachedValue = await _database.StringGetAsync(responseKey);
        if (cachedValue.IsNull)
        {
            return null;
        }

        var cachedResponse = JsonSerializer.Deserialize<CachedResponse>(cachedValue.ToString(), JsonOptions)
            ?? throw new InvalidOperationException("The cached idempotent response is invalid.");

        if (!string.Equals(cachedResponse.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new IdempotencyKeyReuseException();
        }

        return cachedResponse;
    }

    private async Task<CachedResponse> CaptureResponseAsync(HttpContext context)
    {
        var originalBody = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        try
        {
            await next(context);
            return new CachedResponse(
                context.Response.StatusCode,
                context.Response.Headers
                    .Where(header => !IsExcludedHeader(header.Key))
                    .ToDictionary(
                        header => header.Key,
                        header => header.Value.Select(value => value ?? string.Empty).ToArray()),
                responseBody.ToArray());
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static async Task WriteResponseAsync(HttpContext context, CachedResponse response)
    {
        context.Response.StatusCode = response.StatusCode;
        foreach (var (headerName, headerValues) in response.Headers)
        {
            context.Response.Headers[headerName] = headerValues;
        }

        await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
    }

    private static async Task<string> GetRequestHashAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        var hash = await SHA256.HashDataAsync(context.Request.Body, context.RequestAborted);
        context.Request.Body.Position = 0;
        return Convert.ToHexString(hash);
    }

    private static bool IsBookCatalogRead(HttpRequest request, out string cacheKey)
    {
        cacheKey = string.Empty;
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var path = request.Path.Value?.TrimEnd('/');
        if (string.Equals(path, "/books", StringComparison.OrdinalIgnoreCase)
            || IsBookIdPath(path))
        {
            var requestPath = $"{path}{request.QueryString}";
            var pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestPath)));
            cacheKey = $"{KeyPrefix}:catalog:{pathHash}";
            return true;
        }

        return false;
    }

    private static bool IsLoanCreation(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && string.Equals(request.Path.Value?.TrimEnd('/'), "/loans", StringComparison.OrdinalIgnoreCase);

    private static bool IsCatalogMutation(HttpRequest request)
    {
        var path = request.Path.Value?.TrimEnd('/');
        if (string.Equals(path, "/books", StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsPost(request.Method);
        }

        if (IsBookIdPath(path))
        {
            return HttpMethods.IsPatch(request.Method) || HttpMethods.IsDelete(request.Method);
        }

        return IsLoanCreation(request)
            || (HttpMethods.IsPost(request.Method) && IsLoanReturnOrCancellationPath(path));
    }

    private static bool IsBookIdPath(string? path) =>
        path is not null
        && path.StartsWith("/books/", StringComparison.OrdinalIgnoreCase)
        && Guid.TryParse(path.AsSpan("/books/".Length), out _)
        && path.IndexOf('/', "/books/".Length) < 0;

    private static bool IsLoanReturnOrCancellationPath(string? path)
    {
        if (path is null || !path.StartsWith("/loans/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 3
            && Guid.TryParse(segments[1], out _)
            && (string.Equals(segments[2], "return", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segments[2], "cancel", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExcludedHeader(string headerName) =>
        headerName.Equals("Date", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("Server", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("Upgrade", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
        || headerName.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase);

    private static bool IsSuccessful(int statusCode) =>
        statusCode is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices;

    private sealed record CachedResponse(
        int StatusCode,
        Dictionary<string, string[]> Headers,
        byte[] Body,
        string? RequestHash = null);

    private sealed class IdempotencyKeyReuseException()
        : InvalidOperationException("The Idempotency-Key has already been used with a different request.");
}
