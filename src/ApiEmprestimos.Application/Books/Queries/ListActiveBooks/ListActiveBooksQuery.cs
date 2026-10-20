using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Books.Queries.ListActiveBooks;

public sealed record ListActiveBooksQuery : IRequest<IReadOnlyList<BookResponseDto>>;
