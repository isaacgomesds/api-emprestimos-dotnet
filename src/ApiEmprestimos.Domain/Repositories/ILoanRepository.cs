using ApiEmprestimos.Domain.Entities;

namespace ApiEmprestimos.Domain.Repositories;

public interface ILoanRepository
{
    Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasActiveLoanAsync(
        Guid userId,
        Guid bookId,
        CancellationToken cancellationToken = default);
    Task<bool> HasActiveLoansForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveLoansForBookAsync(Guid bookId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Loan>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Loan>> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default);
    Task RegisterAsync(Loan loan, CancellationToken cancellationToken = default);
    Task<bool> TryCloseActiveLoanAsync(
        Guid id,
        LoanStatus status,
        DateTime? returnDateUtc,
        CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
