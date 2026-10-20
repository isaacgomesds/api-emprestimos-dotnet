using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using ApiEmprestimos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiEmprestimos.Infrastructure.Repositories;

public sealed class BookRepository(LibraryDbContext dbContext) : IBookRepository
{
    public Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Books.SingleOrDefaultAsync(book => book.Id == id, cancellationToken);

    public Task<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn);
        var normalizedIsbn = isbn.Trim();

        return dbContext.Books.SingleOrDefaultAsync(
            book => book.Isbn == normalizedIsbn,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Book>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Books
            .AsNoTracking()
            .Where(book => book.IsActive)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        await dbContext.Books.AddAsync(book, cancellationToken);
    }

    public Task UpdateAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        dbContext.Books.Update(book);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
