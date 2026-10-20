using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Loans.Commands.ReturnLoan;

public sealed record ReturnLoanCommand(
    Guid LoanId,
    string Actor,
    string CorrelationId) : IRequest<LoanResponseDto>;
