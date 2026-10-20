namespace ApiEmprestimos.Domain.Entities;

public sealed class IdempotentRequest
{
    private IdempotentRequest()
    {
    }

    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public static IdempotentRequest Create(string key, int statusCode, string responseBody)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (statusCode < 100 || statusCode > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Status code must be a valid HTTP status code.");
        }

        return new IdempotentRequest
        {
            Id = Guid.NewGuid(),
            Key = key.Trim(),
            StatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateResponse(int statusCode, string responseBody)
    {
        if (statusCode < 100 || statusCode > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Status code must be a valid HTTP status code.");
        }

        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
