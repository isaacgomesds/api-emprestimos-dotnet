using System.Text.Json;
using ApiEmprestimos.Domain.Entities;

namespace ApiEmprestimos.Application.Common;

internal static class AuditEventFactory
{
    public static AuditEvent Create(
        string entityName,
        Guid entityId,
        string action,
        string actor,
        string correlationId,
        object details) =>
        AuditEvent.Create(
            entityName,
            entityId.ToString(),
            action,
            actor,
            correlationId,
            JsonSerializer.Serialize(details));
}
