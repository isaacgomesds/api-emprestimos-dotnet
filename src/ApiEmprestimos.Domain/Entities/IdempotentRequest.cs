namespace ApiEmprestimos.Domain.Entities;

public sealed class IdempotentRequest
{
    private IdempotentRequest()
    {
    }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public static IdempotentRequest CreatePending(string key, string requestHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        return new IdempotentRequest
        {
            Id = Guid.NewGuid(),
            Key = key.Trim(),
            RequestHash = requestHash,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Complete(int statusCode, string responseBody)
    {
        if (statusCode < 100 || statusCode > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Status code must be a valid HTTP status code.");
        }

        ArgumentNullException.ThrowIfNull(responseBody);
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
