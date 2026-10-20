namespace ApiEmprestimos.Api.Endpoints;

internal static class RequestMetadata
{
    public static string GetActor(HttpContext context) =>
        GetHeader(context, "X-Actor", "anonymous", 200);

    public static string GetCorrelationId(HttpContext context) =>
        GetHeader(context, "X-Correlation-ID", context.TraceIdentifier, 100);

    private static string GetHeader(HttpContext context, string name, string fallback, int maxLength)
    {
        var value = context.Request.Headers[name].ToString().Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException($"The {name} header cannot exceed {maxLength} characters.");
        }

        return value;
    }
}
