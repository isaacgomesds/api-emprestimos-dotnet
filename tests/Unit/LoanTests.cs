using ApiEmprestimos.Domain.Entities;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class LoanTests
{
    [Fact]
    public void Create_normalizes_dates_and_starts_as_active()
    {
        var loanDate = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);
        var dueDate = loanDate.AddDays(14);

        var loan = Loan.Create(Guid.NewGuid(), Guid.NewGuid(), loanDate, dueDate);

        loan.LoanDateUtc.Kind.Should().Be(DateTimeKind.Utc);
        loan.DueDateUtc.Kind.Should().Be(DateTimeKind.Utc);
        loan.Status.Should().Be(LoanStatus.Active);
        loan.IsOpen().Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_due_date_before_loan_date()
    {
        var loanDate = DateTime.UtcNow;

        var act = () => Loan.Create(Guid.NewGuid(), Guid.NewGuid(), loanDate, loanDate.AddDays(-1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Due date cannot be earlier than the loan date.*");
    }

    [Fact]
    public void Return_closes_active_loan_and_rejects_a_second_return()
    {
        var loanDate = DateTime.UtcNow;
        var loan = Loan.Create(Guid.NewGuid(), Guid.NewGuid(), loanDate, loanDate.AddDays(14));

        loan.Return(loanDate.AddDays(2));

        loan.Status.Should().Be(LoanStatus.Returned);
        loan.ReturnDateUtc.Should().Be(loanDate.AddDays(2));
        loan.IsOpen().Should().BeFalse();
        var act = () => loan.Return(loanDate.AddDays(3));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only active loans can be returned.");
    }

    [Fact]
    public void Cancel_closes_active_loan_without_a_return_date()
    {
        var now = DateTime.UtcNow;
        var loan = Loan.Create(Guid.NewGuid(), Guid.NewGuid(), now, now.AddDays(14));

        loan.Cancel();

        loan.Status.Should().Be(LoanStatus.Cancelled);
        loan.ReturnDateUtc.Should().BeNull();
        loan.IsOpen().Should().BeFalse();
    }
}
