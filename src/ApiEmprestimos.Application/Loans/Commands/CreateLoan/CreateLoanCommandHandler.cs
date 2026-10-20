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
    IAuditEventRepository auditEventRepository) : IRequestHandler<CreateLoanCommand, LoanResponseDto>
{
    public async Task<LoanResponseDto> Handle(
        CreateLoanCommand request,
        CancellationToken cancellationToken)
    {
        var book = await bookRepository.GetByIdAsync(request.BookId, cancellationToken)
            ?? throw new KeyNotFoundException($"Book '{request.BookId}' was not found.");
        if (await userRepository.GetByIdAsync(request.UserId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"User '{request.UserId}' was not found.");
        }

        if (await loanRepository.HasActiveLoanAsync(request.UserId, request.BookId, cancellationToken))
        {
            throw new InvalidOperationException("The user already has an active loan for this book.");
        }

        if (!book.TryReserveCopy())
        {
            throw new InvalidOperationException("The book is inactive or has no available copies.");
        }

        var loan = Loan.Create(request.BookId, request.UserId, request.LoanDateUtc, request.DueDateUtc);
        await bookRepository.UpdateAsync(book, cancellationToken);
        await loanRepository.RegisterAsync(loan, cancellationToken);
        await auditEventRepository.LogAsync(
            AuditEventFactory.Create(
                nameof(Loan),
                loan.Id,
                "LoanCreated",
                request.Actor,
                request.CorrelationId,
                new { loan.BookId, loan.UserId, loan.LoanDateUtc, loan.DueDateUtc }),
            cancellationToken);
        await loanRepository.SaveChangesAsync(cancellationToken);

        return new LoanResponseDto(
            loan.Id,
            loan.BookId,
            loan.UserId,
            loan.LoanDateUtc,
            loan.DueDateUtc,
            loan.ReturnDateUtc,
            loan.Status);
    }
}
