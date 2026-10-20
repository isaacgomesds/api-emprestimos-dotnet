using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Books.Commands.UpdateBook;

public sealed record UpdateBookCommand(
    Guid Id,
    string Title,
    string Author,
    int TotalCopies,
    string Actor,
    string CorrelationId) : IRequest<BookResponseDto>;
