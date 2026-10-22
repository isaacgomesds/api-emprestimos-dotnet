using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Loans.Commands.ReturnLoan;

public sealed class ReturnLoanCommandHandler(
    ILoanRepository loanRepository,
    IBookRepository bookRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ReturnLoanCommand, LoanResponseDto>
{
    public async Task<LoanResponseDto> Handle(
        ReturnLoanCommand request,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var loan = await loanRepository.GetByIdAsync(request.LoanId, transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Loan '{request.LoanId}' was not found.");

            if (!loan.IsOpen())
            {
                throw new InvalidOperationException("Only active loans can be returned.");
            }

            var returnDateUtc = DateTime.UtcNow;
            loan.Return(returnDateUtc);
            if (!await loanRepository.TryCloseActiveLoanAsync(
                    loan.Id,
                    LoanStatus.Returned,
                    returnDateUtc,
                    transactionCancellationToken))
            {
                throw new InvalidOperationException("Only active loans can be returned.");
            }

            if (!await bookRepository.TryIncrementAvailableCopiesAsync(
                    loan.BookId,
                    transactionCancellationToken))
            {
                throw new InvalidOperationException("The book inventory cannot be incremented.");
            }

            await auditEventRepository.LogAsync(
                AuditEventFactory.Create(
                    nameof(Loan),
                    loan.Id,
                    "LoanReturned",
                    request.Actor,
                    request.CorrelationId,
                    new { loan.BookId, loan.UserId, ReturnDateUtc = returnDateUtc }),
                transactionCancellationToken);

            return new LoanResponseDto(
                loan.Id,
                loan.BookId,
                loan.UserId,
                loan.LoanDateUtc,
                loan.DueDateUtc,
                returnDateUtc,
                LoanStatus.Returned);
        }, cancellationToken);
    }
}
