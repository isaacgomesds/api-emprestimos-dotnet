using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Books.Commands.UpdateBook;

public sealed class UpdateBookCommandHandler(
    IBookRepository bookRepository,
    IAuditEventRepository auditEventRepository) : IRequestHandler<UpdateBookCommand, BookResponseDto>
{
    public async Task<BookResponseDto> Handle(
        UpdateBookCommand request,
        CancellationToken cancellationToken)
    {
        var book = await bookRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Book '{request.Id}' was not found.");

        if (request.Title is not null || request.Author is not null)
        {
            book.UpdateDetails(request.Title ?? book.Title, request.Author ?? book.Author);
        }

        if (request.TotalCopies is not null)
        {
            book.UpdateStock(request.TotalCopies.Value);
        }
        await bookRepository.UpdateAsync(book, cancellationToken);
        await auditEventRepository.LogAsync(
            AuditEventFactory.Create(
                nameof(Book),
                book.Id,
                "BookUpdated",
                request.Actor,
                request.CorrelationId,
                new { book.Title, book.Author, book.TotalCopies, book.AvailableCopies }),
            cancellationToken);
        await bookRepository.SaveChangesAsync(cancellationToken);

        return new BookResponseDto(
            book.Id,
            book.Title,
            book.Isbn,
            book.Author,
            book.TotalCopies,
            book.AvailableCopies,
            book.IsActive);
    }
}
