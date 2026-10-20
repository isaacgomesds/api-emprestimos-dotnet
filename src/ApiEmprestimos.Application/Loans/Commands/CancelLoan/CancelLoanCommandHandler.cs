using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Loans.Commands.CancelLoan;

public sealed class CancelLoanCommandHandler(
    ILoanRepository loanRepository,
    IBookRepository bookRepository,
    IAuditEventRepository auditEventRepository) : IRequestHandler<CancelLoanCommand, LoanResponseDto>
{
    public async Task<LoanResponseDto> Handle(
        CancelLoanCommand request,
        CancellationToken cancellationToken)
    {
        var loan = await loanRepository.GetByIdAsync(request.LoanId, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan '{request.LoanId}' was not found.");
        var book = await bookRepository.GetByIdAsync(loan.BookId, cancellationToken)
            ?? throw new KeyNotFoundException($"Book '{loan.BookId}' was not found.");

        if (!loan.IsOpen())
        {
            throw new InvalidOperationException("Only active loans can be cancelled.");
        }

        book.ReleaseCopy();
        loan.Cancel();
        await bookRepository.UpdateAsync(book, cancellationToken);
        await auditEventRepository.LogAsync(
            AuditEventFactory.Create(
                nameof(Loan),
                loan.Id,
                "LoanCancelled",
                request.Actor,
                request.CorrelationId,
                new { loan.BookId, loan.UserId }),
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
