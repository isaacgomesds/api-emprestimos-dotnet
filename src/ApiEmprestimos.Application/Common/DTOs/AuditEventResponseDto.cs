namespace ApiEmprestimos.Application.Common.DTOs;

/// <summary>Represents an audit event returned by the application.</summary>
public sealed record AuditEventResponseDto(
    Guid Id,
    string EntityName,
    string EntityId,
    string Action,
    string Actor,
    DateTime TimestampUtc,
    string CorrelationId,
    string DetailsJson);
