namespace ApiEmprestimos.Api.Middleware;

public sealed class CorrelationLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationLoggingMiddleware> logger)
    {
        var requestedCorrelationId = context.Request.Headers["X-Correlation-ID"].ToString().Trim();
        var correlationId = requestedCorrelationId.Length is > 0 and <= 100
            ? requestedCorrelationId
            : context.TraceIdentifier;

        context.Response.Headers["X-Correlation-ID"] = correlationId;
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = correlationId
        }))
        {
            await next(context);
        }
    }
}
