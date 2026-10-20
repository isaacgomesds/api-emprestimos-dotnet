using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Users.Queries.GetUserLoanHistory;

public sealed class GetUserLoanHistoryQueryHandler(
    IUserRepository userRepository,
    ILoanRepository loanRepository) : IRequestHandler<GetUserLoanHistoryQuery, IReadOnlyList<LoanResponseDto>>
{
    public async Task<IReadOnlyList<LoanResponseDto>> Handle(
        GetUserLoanHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (await userRepository.GetByIdAsync(request.UserId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"User '{request.UserId}' was not found.");
        }

        var loans = await loanRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        return loans.Select(loan => new LoanResponseDto(
            loan.Id,
            loan.BookId,
            loan.UserId,
            loan.LoanDateUtc,
            loan.DueDateUtc,
            loan.ReturnDateUtc,
            loan.Status)).ToArray();
    }
}
