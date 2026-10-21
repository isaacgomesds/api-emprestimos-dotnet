using ApiEmprestimos.Application.Users.Queries.GetAllUsers;
using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class GetAllUsersQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_user_details_from_the_repository()
    {
        var users = new[]
        {
            User.Create("Ada Lovelace", "ada@example.test"),
            User.Create("Grace Hopper", "grace@example.test")
        };
        var handler = new GetAllUsersQueryHandler(new UserRepositoryStub(users));

        var result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(users.Select(user => new
        {
            user.Id,
            user.Name,
            user.Email,
            CreatedAtUtc = (DateTimeOffset)user.CreatedAtUtc
        }));
    }

    private sealed class UserRepositoryStub(IReadOnlyList<User> users) : IUserRepository
    {
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(users);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
