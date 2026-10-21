using ApiEmprestimos.Application.Common.DTOs;
using MediatR;

namespace ApiEmprestimos.Application.Users.Queries.GetAllUsers;

public sealed record GetAllUsersQuery : IRequest<IReadOnlyList<UserResponseDto>>;
