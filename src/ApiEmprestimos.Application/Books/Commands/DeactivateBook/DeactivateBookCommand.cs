using MediatR;

namespace ApiEmprestimos.Application.Books.Commands.DeactivateBook;

public sealed record DeactivateBookCommand(
    Guid Id,
    string Actor,
    string CorrelationId) : IRequest;
