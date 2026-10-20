using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Books.Queries.GetBookById;

public sealed class GetBookByIdQueryHandler(IBookRepository bookRepository)
    : IRequestHandler<GetBookByIdQuery, BookResponseDto>
{
    public async Task<BookResponseDto> Handle(
        GetBookByIdQuery request,
        CancellationToken cancellationToken)
    {
        var book = await bookRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Book '{request.Id}' was not found.");

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
