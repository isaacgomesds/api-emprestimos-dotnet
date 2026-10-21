using ApiEmprestimos.Domain.Entities;

namespace ApiEmprestimos.Application.Common.DTOs;

/// <summary>Represents a loan in a user's history, including its book details.</summary>
public sealed record UserLoanHistoryResponseDto(
    Guid Id,
    Guid BookId,
    string BookTitle,
    string BookAuthor,
    string Isbn,
    Guid UserId,
    DateTimeOffset LoanDateUtc,
    DateTimeOffset DueDateUtc,
    DateTimeOffset? ReturnDateUtc,
    LoanStatus Status);
