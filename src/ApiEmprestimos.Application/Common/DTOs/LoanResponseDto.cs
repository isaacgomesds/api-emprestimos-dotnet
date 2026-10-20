using ApiEmprestimos.Domain.Entities;

namespace ApiEmprestimos.Application.Common.DTOs;

/// <summary>Represents a loan returned by the application.</summary>
public sealed record LoanResponseDto(
    Guid Id,
    Guid BookId,
    Guid UserId,
    DateTimeOffset LoanDateUtc,
    DateTimeOffset DueDateUtc,
    DateTimeOffset? ReturnDateUtc,
    LoanStatus Status);
