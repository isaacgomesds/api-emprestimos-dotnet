using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Loans.Commands.CreateLoan;

public sealed class CreateLoanCommandHandler(
    IBookRepository bookRepository,
    IUserRepository userRepository,
    ILoanRepository loanRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateLoanCommand, LoanResponseDto>
{
    public async Task<LoanResponseDto> Handle(
        CreateLoanCommand request,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var book = await bookRepository.GetByIdAsync(request.BookId, transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Book '{request.BookId}' was not found.");
            if (await userRepository.GetByIdAsync(request.UserId, transactionCancellationToken) is null)
            {
                throw new KeyNotFoundException($"User '{request.UserId}' was not found.");
            }

            if (await loanRepository.HasActiveLoanAsync(
                    request.UserId,
                    request.BookId,
                    transactionCancellationToken))
            {
                throw new InvalidOperationException("The user already has an active loan for this book.");
            }

            if (!await bookRepository.TryReserveCopyAsync(book.Id, transactionCancellationToken))
            {
                throw new InvalidOperationException("The book is inactive or has no available copies.");
            }

            var loan = Loan.Create(request.BookId, request.UserId, request.LoanDateUtc, request.DueDateUtc);
            await loanRepository.RegisterAsync(loan, transactionCancellationToken);
            await auditEventRepository.LogAsync(
                AuditEventFactory.Create(
                    nameof(Loan),
                    loan.Id,
                    "LoanCreated",
                    request.Actor,
                    request.CorrelationId,
                    new { loan.BookId, loan.UserId, loan.LoanDateUtc, loan.DueDateUtc }),
                transactionCancellationToken);
            await loanRepository.SaveChangesAsync(transactionCancellationToken);

            return new LoanResponseDto(
                loan.Id,
                loan.BookId,
                loan.UserId,
                loan.LoanDateUtc,
                loan.DueDateUtc,
                loan.ReturnDateUtc,
                loan.Status);
        }, cancellationToken);
    }
}
