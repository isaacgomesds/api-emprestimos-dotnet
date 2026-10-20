using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Books.Commands.CreateBook;

public sealed record CreateBookCommand(
    string Title,
    string Isbn,
    string Author,
    int TotalCopies,
    string Actor,
    string CorrelationId) : IRequest<BookResponseDto>;
