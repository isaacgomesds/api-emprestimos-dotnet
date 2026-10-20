using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Loans.Queries.GetBookLoanHistory;

public sealed record GetBookLoanHistoryQuery(Guid BookId) : IRequest<IReadOnlyList<LoanResponseDto>>;
