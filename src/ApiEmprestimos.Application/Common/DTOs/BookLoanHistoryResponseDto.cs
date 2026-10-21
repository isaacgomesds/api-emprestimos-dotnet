using ApiEmprestimos.Domain.Entities;

namespace ApiEmprestimos.Application.Common.DTOs;

/// <summary>Represents a loan in a book's history, including borrower details.</summary>
public sealed record BookLoanHistoryResponseDto(
    Guid Id,
    Guid BookId,
    Guid UserId,
    string UserName,
    string UserEmail,
    DateTimeOffset LoanDateUtc,
    DateTimeOffset DueDateUtc,
    DateTimeOffset? ReturnDateUtc,
    LoanStatus Status);
