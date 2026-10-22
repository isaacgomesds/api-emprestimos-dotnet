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

    public async Task<BookReservationResult> TryReserveCopyAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var reservedRows = await dbContext.Books
                .Where(book => book.Id == id && book.IsActive && book.AvailableCopies > 0)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        book => book.AvailableCopies,
                        book => book.AvailableCopies - 1),
                    cancellationToken);

            if (reservedRows == 1)
            {
                return BookReservationResult.Reserved;
            }

            var bookState = await dbContext.Books
                .AsNoTracking()
                .Where(book => book.Id == id)
                .Select(book => new { book.IsActive, book.AvailableCopies })
                .SingleOrDefaultAsync(cancellationToken);

            if (bookState is null)
            {
                return BookReservationResult.BookNotFound;
            }

            if (!bookState.IsActive)
            {
                return BookReservationResult.BookInactive;
            }

            if (bookState.AvailableCopies <= 0)
            {
                return BookReservationResult.NoAvailableCopies;
            }
        }
    }

    public async Task<bool> TryUpdateStockAsync(
        Guid id,
        int totalCopies,
        CancellationToken cancellationToken = default)
    {
        if (totalCopies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCopies), "Total copies cannot be negative.");
        }

        return await dbContext.Books
            .Where(book => book.Id == id && book.TotalCopies - book.AvailableCopies <= totalCopies)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(book => book.TotalCopies, totalCopies)
                    .SetProperty(
                        book => book.AvailableCopies,
                        book => totalCopies - (book.TotalCopies - book.AvailableCopies)),
                cancellationToken) == 1;
    }

    public async Task<(int TotalCopies, int AvailableCopies)?> GetStockAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var stock = await dbContext.Books
            .AsNoTracking()
            .Where(book => book.Id == id)
            .Select(book => new { book.TotalCopies, book.AvailableCopies })
            .SingleOrDefaultAsync(cancellationToken);

        return stock is null ? null : (stock.TotalCopies, stock.AvailableCopies);
    }

    public async Task<bool> TryIncrementAvailableCopiesAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await dbContext.Books
            .Where(book => book.Id == id && book.AvailableCopies < book.TotalCopies)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    book => book.AvailableCopies,
                    book => book.AvailableCopies + 1),
                cancellationToken) == 1;

    public async Task AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        await dbContext.Books.AddAsync(book, cancellationToken);
    }

    public Task UpdateAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (dbContext.Entry(book).State == EntityState.Detached)
        {
            dbContext.Books.Update(book);
        }

        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
