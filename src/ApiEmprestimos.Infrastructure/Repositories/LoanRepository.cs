using ApiEmprestimos.Domain.Entities;
using ApiEmprestimos.Domain.Repositories;
using ApiEmprestimos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiEmprestimos.Infrastructure.Repositories;

public sealed class LoanRepository(LibraryDbContext dbContext) : ILoanRepository
{
    public Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Loans.SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);

    public Task<bool> HasActiveLoanAsync(
        Guid userId,
        Guid bookId,
        CancellationToken cancellationToken = default) =>
        dbContext.Loans.AnyAsync(
            loan => loan.UserId == userId
                && loan.BookId == bookId
                && loan.Status == LoanStatus.Active,
            cancellationToken);

    public Task<bool> HasActiveLoansForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.Loans.AnyAsync(
            loan => loan.UserId == userId && loan.Status == LoanStatus.Active,
            cancellationToken);

    public Task<bool> HasActiveLoansForBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default) =>
        dbContext.Loans.AnyAsync(
            loan => loan.BookId == bookId && loan.Status == LoanStatus.Active,
            cancellationToken);

    public async Task<IReadOnlyList<Loan>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Loans
            .AsNoTracking()
            .Include(loan => loan.Book)
            .Where(loan => loan.UserId == userId)
            .OrderByDescending(loan => loan.LoanDateUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Loan>> GetByBookIdAsync(
        Guid bookId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Loans
            .AsNoTracking()
            .Include(loan => loan.User)
            .Where(loan => loan.BookId == bookId)
            .OrderByDescending(loan => loan.LoanDateUtc)
            .ToListAsync(cancellationToken);

    public async Task RegisterAsync(Loan loan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loan);
        await dbContext.Loans.AddAsync(loan, cancellationToken);
    }

    public async Task<bool> TryCloseActiveLoanAsync(
        Guid id,
        LoanStatus status,
        DateTime? returnDateUtc,
        CancellationToken cancellationToken = default)
    {
        if (status is not (LoanStatus.Returned or LoanStatus.Cancelled))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "A loan can only be closed as returned or cancelled.");
        }

        if ((status == LoanStatus.Returned) != returnDateUtc.HasValue)
        {
            throw new ArgumentException("Only returned loans can have a return date.", nameof(returnDateUtc));
        }

        return await dbContext.Loans
            .Where(loan => loan.Id == id && loan.Status == LoanStatus.Active)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(loan => loan.Status, status)
                    .SetProperty(loan => loan.ReturnDateUtc, returnDateUtc),
                cancellationToken) == 1;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
