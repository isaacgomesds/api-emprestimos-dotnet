using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Loans.Commands.CancelLoan;

public sealed record CancelLoanCommand(
    Guid LoanId,
    string Actor,
    string CorrelationId) : IRequest<LoanResponseDto>;
