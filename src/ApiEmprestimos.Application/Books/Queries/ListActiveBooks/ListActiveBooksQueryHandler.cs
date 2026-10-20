using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Books.Queries.ListActiveBooks;

public sealed class ListActiveBooksQueryHandler(IBookRepository bookRepository)
    : IRequestHandler<ListActiveBooksQuery, IReadOnlyList<BookResponseDto>>
{
    public async Task<IReadOnlyList<BookResponseDto>> Handle(
        ListActiveBooksQuery request,
        CancellationToken cancellationToken)
    {
        var books = await bookRepository.ListActiveAsync(cancellationToken);
        return books.Select(book => new BookResponseDto(
            book.Id,
            book.Title,
            book.Isbn,
            book.Author,
            book.TotalCopies,
            book.AvailableCopies,
            book.IsActive)).ToArray();
    }
}
