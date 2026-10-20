using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Users.Queries.GetUserLoanHistory;

public sealed record GetUserLoanHistoryQuery(Guid UserId) : IRequest<IReadOnlyList<LoanResponseDto>>;
