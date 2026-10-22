using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Books.Commands.UpdateBook;

public sealed class UpdateBookCommandHandler(
    IBookRepository bookRepository,
    IAuditEventRepository auditEventRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateBookCommand, BookResponseDto>
{
    public async Task<BookResponseDto> Handle(
        UpdateBookCommand request,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var book = await bookRepository.GetByIdAsync(request.Id, transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Book '{request.Id}' was not found.");

            if (request.Title is not null || request.Author is not null)
            {
                book.UpdateDetails(request.Title ?? book.Title, request.Author ?? book.Author);
            }

            if (request.TotalCopies is not null
                && !await bookRepository.TryUpdateStockAsync(
                    book.Id,
                    request.TotalCopies.Value,
                    transactionCancellationToken))
            {
                throw new InvalidOperationException(
                    "Total copies cannot be less than the number of copies currently on loan.");
            }

            var stock = await bookRepository.GetStockAsync(book.Id, transactionCancellationToken)
                ?? throw new KeyNotFoundException($"Book '{book.Id}' was not found.");

            await auditEventRepository.LogAsync(
                AuditEventFactory.Create(
                    nameof(Book),
                    book.Id,
                    "BookUpdated",
                    request.Actor,
                    request.CorrelationId,
                    new { book.Title, book.Author, stock.TotalCopies, stock.AvailableCopies }),
                transactionCancellationToken);

            return new BookResponseDto(
                book.Id,
                book.Title,
                book.Isbn,
                book.Author,
                stock.TotalCopies,
                stock.AvailableCopies,
                book.IsActive);
        }, cancellationToken);
    }
}
