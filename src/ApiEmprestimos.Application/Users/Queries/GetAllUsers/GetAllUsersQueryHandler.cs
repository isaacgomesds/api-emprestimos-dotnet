using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Users.Queries.GetAllUsers;

public sealed class GetAllUsersQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetAllUsersQuery, IReadOnlyList<UserResponseDto>>
{
    public async Task<IReadOnlyList<UserResponseDto>> Handle(
        GetAllUsersQuery request,
        CancellationToken cancellationToken)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);
        return users.Select(user => new UserResponseDto(
            user.Id,
            user.Name,
            user.Email,
            user.CreatedAtUtc)).ToArray();
    }
}
