namespace ApiEmprestimos.Domain.Entities;

public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    public Guid Id { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public DateTime TimestampUtc { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string DetailsJson { get; private set; } = "{}";

    public static AuditEvent Create(
        string entityName,
        string entityId,
        string action,
        string actor,
        string correlationId,
        string detailsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            EntityName = entityName.Trim(),
            EntityId = entityId.Trim(),
            Action = action.Trim(),
            Actor = actor.Trim(),
            TimestampUtc = DateTime.UtcNow,
            CorrelationId = correlationId.Trim(),
            DetailsJson = string.IsNullOrWhiteSpace(detailsJson) ? "{}" : detailsJson
        };
    }
}
