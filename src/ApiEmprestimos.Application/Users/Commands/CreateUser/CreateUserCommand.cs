using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Name,
    string Email,
    string Actor,
    string CorrelationId) : IRequest<UserResponseDto>;
