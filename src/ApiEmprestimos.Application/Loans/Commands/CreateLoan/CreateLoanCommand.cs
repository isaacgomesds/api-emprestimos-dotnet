using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Loans.Commands.CreateLoan;

public sealed record CreateLoanCommand(
    Guid BookId,
    Guid UserId,
    DateTime LoanDateUtc,
    DateTime DueDateUtc,
    string IdempotencyKey,
    string Actor,
    string CorrelationId) : IRequest<LoanResponseDto>;
