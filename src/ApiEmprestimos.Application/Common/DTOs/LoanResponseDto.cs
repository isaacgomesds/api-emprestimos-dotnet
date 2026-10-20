using ApiEmprestimos.Domain.Entities;

namespace ApiEmprestimos.Application.Common.DTOs;

public sealed record LoanResponseDto(
    Guid Id,
    Guid BookId,
    Guid UserId,
    DateTime LoanDateUtc,
    DateTime DueDateUtc,
    DateTime? ReturnDateUtc,
    LoanStatus Status);
