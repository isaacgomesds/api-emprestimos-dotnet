using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Loans.Commands.CancelLoan;

public sealed class CancelLoanCommandHandler(
    ILoanRepository loanRepository,
    IBookRepository bookRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelLoanCommand, LoanResponseDto>
{
    public async Task<LoanResponseDto> Handle(
        CancelLoanCommand request,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var loan = await loanRepository.GetByIdAsync(request.LoanId, transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Loan '{request.LoanId}' was not found.");

            if (!loan.IsOpen())
            {
                throw new InvalidOperationException("Only active loans can be cancelled.");
            }

            loan.Cancel();
            if (!await loanRepository.TryCloseActiveLoanAsync(
                    loan.Id,
                    LoanStatus.Cancelled,
                    returnDateUtc: null,
                    cancellationToken: transactionCancellationToken))
            {
                throw new InvalidOperationException("Only active loans can be cancelled.");
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
                    "LoanCancelled",
                    request.Actor,
                    request.CorrelationId,
                    new { loan.BookId, loan.UserId }),
                transactionCancellationToken);

            return new LoanResponseDto(
                loan.Id,
                loan.BookId,
                loan.UserId,
                loan.LoanDateUtc,
                loan.DueDateUtc,
                null,
                LoanStatus.Cancelled);
        }, cancellationToken);
    }
}
