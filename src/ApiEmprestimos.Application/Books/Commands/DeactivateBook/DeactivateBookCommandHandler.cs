using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Books.Commands.DeactivateBook;

public sealed class DeactivateBookCommandHandler(
    IBookRepository bookRepository,
    ILoanRepository loanRepository,
    IAuditEventRepository auditEventRepository) : IRequestHandler<DeactivateBookCommand>
{
    public async Task Handle(DeactivateBookCommand request, CancellationToken cancellationToken)
    {
        var book = await bookRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Book '{request.Id}' was not found.");

        if (await loanRepository.HasActiveLoansForBookAsync(book.Id, cancellationToken))
        {
            throw new InvalidOperationException("A book with active loans cannot be deactivated.");
        }

        book.Deactivate();
        await bookRepository.UpdateAsync(book, cancellationToken);
        await auditEventRepository.LogAsync(
            AuditEventFactory.Create(
                nameof(Book),
                book.Id,
                "BookDeactivated",
                request.Actor,
                request.CorrelationId,
                new { book.Title, book.Isbn }),
            cancellationToken);
        await bookRepository.SaveChangesAsync(cancellationToken);
    }
}
