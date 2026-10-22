using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Exceptions;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Books.Commands.CreateBook;

public sealed class CreateBookCommandHandler(
    IBookRepository bookRepository,
    IAuditEventRepository auditEventRepository) : IRequestHandler<CreateBookCommand, BookResponseDto>
{
    public async Task<BookResponseDto> Handle(
        CreateBookCommand request,
        CancellationToken cancellationToken)
    {
        if (await bookRepository.GetByIsbnAsync(request.Isbn, cancellationToken) is not null)
        {
            throw new BookIsbnAlreadyExistsException();
        }

        var book = Book.Create(request.Title, request.Isbn, request.Author, request.TotalCopies);
        await bookRepository.AddAsync(book, cancellationToken);
        await auditEventRepository.LogAsync(
            AuditEventFactory.Create(
                nameof(Book),
                book.Id,
                "BookCreated",
                request.Actor,
                request.CorrelationId,
                new { book.Title, book.Isbn, book.Author, book.TotalCopies }),
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
