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
            .Where(loan => loan.UserId == userId)
            .OrderByDescending(loan => loan.LoanDateUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Loan>> GetByBookIdAsync(
        Guid bookId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Loans
            .AsNoTracking()
            .Where(loan => loan.BookId == bookId)
            .OrderByDescending(loan => loan.LoanDateUtc)
            .ToListAsync(cancellationToken);

    public async Task RegisterAsync(Loan loan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loan);
        await dbContext.Loans.AddAsync(loan, cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
