using ApiEmprestimos.Application.Common;
using ApiEmprestimos.Application.Common.DTOs;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using MediatR;

namespace ApiEmprestimos.Application.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IAuditEventRepository auditEventRepository) : IRequestHandler<CreateUserCommand, UserResponseDto>
{
    public async Task<UserResponseDto> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        if (await userRepository.GetByEmailAsync(request.Email, cancellationToken) is not null)
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = User.Create(request.Name, request.Email);
        await userRepository.AddAsync(user, cancellationToken);
        await auditEventRepository.LogAsync(
            AuditEventFactory.Create(
                nameof(User),
                user.Id,
                "UserCreated",
                request.Actor,
                request.CorrelationId,
                new { user.Name, user.Email }),
            cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return new UserResponseDto(user.Id, user.Name, user.Email, user.CreatedAtUtc);
    }
}
