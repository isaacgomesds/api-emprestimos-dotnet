using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Books.Queries.GetBookById;

public sealed record GetBookByIdQuery(Guid Id) : IRequest<BookResponseDto>;
