using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApiEmprestimos.Tests.Integration;

[Collection(ApiIntegrationTestCollection.Name)]
public abstract class ApiIntegrationTestBase(ApiIntegrationFixture fixture) : IAsyncLifetime
{
    protected HttpClient Client => fixture.Client;

    public Task InitializeAsync() => fixture.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task FlushRedisAsync()
    {
        var redis = fixture.Services.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
        await redis.GetDatabase().ExecuteAsync("FLUSHDB");
    }

    protected async Task<(Guid BookId, Guid UserId)> SeedBookAndUserAsync(int availableCopies)
    {
        var book = Book.Create("Concurrency Test Book", Guid.NewGuid().ToString("N")[..20], "Test Author", availableCopies);
        var user = User.Create("Integration Test User", $"{Guid.NewGuid():N}@example.test");

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await dbContext.Books.AddAsync(book);
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        return (book.Id, user.Id);
    }

    protected async Task<Guid> SeedUserAsync()
    {
        var user = User.Create("Concurrent Borrower", $"{Guid.NewGuid():N}@example.test");
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    protected async Task<(int AvailableCopies, int LoanCount)> GetBookStateAsync(Guid bookId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var book = await dbContext.Books.SingleAsync(candidate => candidate.Id == bookId);
        var loanCount = await dbContext.Loans.CountAsync(loan => loan.BookId == bookId);
        return (book.AvailableCopies, loanCount);
    }

    protected async Task<(string Title, string Author, string Isbn)> GetBookDetailsAsync(Guid bookId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var book = await dbContext.Books.SingleAsync(candidate => candidate.Id == bookId);
        return (book.Title, book.Author, book.Isbn);
    }

    protected async Task<(string Name, string Email)> GetUserDetailsAsync(Guid userId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.Id == userId);
        return (user.Name, user.Email);
    }
}
