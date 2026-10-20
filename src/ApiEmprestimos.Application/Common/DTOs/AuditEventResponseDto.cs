namespace ApiEmprestimos.Application.Common.DTOs;

public sealed record AuditEventResponseDto(
    Guid Id,
    string EntityName,
    string EntityId,
    string Action,
    string Actor,
    DateTime TimestampUtc,
    string CorrelationId,
    string DetailsJson);
