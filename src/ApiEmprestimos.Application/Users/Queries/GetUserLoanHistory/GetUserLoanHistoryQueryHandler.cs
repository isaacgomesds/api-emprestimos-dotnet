using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Users.Queries.GetUserLoanHistory;

public sealed class GetUserLoanHistoryQueryHandler(
    IUserRepository userRepository,
    ILoanRepository loanRepository) : IRequestHandler<GetUserLoanHistoryQuery, IReadOnlyList<UserLoanHistoryResponseDto>>
{
    public async Task<IReadOnlyList<UserLoanHistoryResponseDto>> Handle(
        GetUserLoanHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (await userRepository.GetByIdAsync(request.UserId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"User '{request.UserId}' was not found.");
        }

        var loans = await loanRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        return loans.Select(loan => new UserLoanHistoryResponseDto(
            loan.Id,
            loan.BookId,
            loan.Book.Title,
            loan.Book.Author,
            loan.Book.Isbn,
            loan.UserId,
            loan.LoanDateUtc,
            loan.DueDateUtc,
            loan.ReturnDateUtc,
            loan.Status)).ToArray();
    }
}
