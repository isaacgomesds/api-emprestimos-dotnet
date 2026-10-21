namespace ApiEmprestimos.Domain.Entities;

public sealed class Loan
{
    private Loan()
    {
    }

    public Guid Id { get; private set; }
    public Guid BookId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime LoanDateUtc { get; private set; }
    public DateTime DueDateUtc { get; private set; }
    public DateTime? ReturnDateUtc { get; private set; }
    public LoanStatus Status { get; private set; }
    public Book Book { get; private set; } = null!;

    public static Loan Create(Guid bookId, Guid userId, DateTime loanDateUtc, DateTime dueDateUtc)
    {
        if (bookId == Guid.Empty)
        {
            throw new ArgumentException("Book identifier is required.", nameof(bookId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        var normalizedLoanDate = EnsureUtc(loanDateUtc);
        var normalizedDueDate = EnsureUtc(dueDateUtc);

        if (normalizedDueDate < normalizedLoanDate)
        {
            throw new ArgumentException("Due date cannot be earlier than the loan date.", nameof(dueDateUtc));
        }

        return new Loan
        {
            Id = Guid.NewGuid(),
            BookId = bookId,
            UserId = userId,
            LoanDateUtc = normalizedLoanDate,
            DueDateUtc = normalizedDueDate,
            Status = LoanStatus.Active
        };
    }

    public void Return(DateTime returnDateUtc)
    {
        if (Status != LoanStatus.Active)
        {
            throw new InvalidOperationException("Only active loans can be returned.");
        }

        var normalizedReturnDate = EnsureUtc(returnDateUtc);
        if (normalizedReturnDate < LoanDateUtc)
        {
            throw new ArgumentException("Return date cannot be earlier than the loan date.", nameof(returnDateUtc));
        }

        ReturnDateUtc = normalizedReturnDate;
        Status = LoanStatus.Returned;
    }

    public void MarkReturned(DateTime returnDateUtc) => Return(returnDateUtc);

    public void Cancel()
    {
        if (Status != LoanStatus.Active)
        {
            throw new InvalidOperationException("Only active loans can be cancelled.");
        }

        Status = LoanStatus.Cancelled;
        ReturnDateUtc = null;
    }

    public bool IsOpen() => Status == LoanStatus.Active;

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => value
        };
    }
}
