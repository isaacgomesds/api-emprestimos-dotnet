using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Loans.Queries.GetBookLoanHistory;

public sealed class GetBookLoanHistoryQueryHandler(
    IBookRepository bookRepository,
    ILoanRepository loanRepository) : IRequestHandler<GetBookLoanHistoryQuery, IReadOnlyList<LoanResponseDto>>
{
    public async Task<IReadOnlyList<LoanResponseDto>> Handle(
        GetBookLoanHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (await bookRepository.GetByIdAsync(request.BookId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"Book '{request.BookId}' was not found.");
        }

        var loans = await loanRepository.GetByBookIdAsync(request.BookId, cancellationToken);
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
