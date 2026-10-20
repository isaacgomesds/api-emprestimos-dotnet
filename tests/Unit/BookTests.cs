using ApiEmprestimos.Domain.Entities;
using FluentAssertions;

namespace ApiEmprestimos.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class BookTests
{
    [Fact]
    public void Create_trims_text_and_initializes_available_stock()
    {
        var book = Book.Create("  Dune  ", "  ISBN-1  ", "  Frank Herbert  ", totalCopies: 3);

        book.Title.Should().Be("Dune");
        book.Isbn.Should().Be("ISBN-1");
        book.Author.Should().Be("Frank Herbert");
        book.TotalCopies.Should().Be(3);
        book.AvailableCopies.Should().Be(3);
        book.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Try_reserve_copy_decrements_only_when_active_stock_exists()
    {
        var book = Book.Create("Dune", "ISBN-1", "Frank Herbert", totalCopies: 1);

        book.TryReserveCopy().Should().BeTrue();
        book.AvailableCopies.Should().Be(0);
        book.TryReserveCopy().Should().BeFalse();
        book.AvailableCopies.Should().Be(0);

        book.ReleaseCopy();
        book.AvailableCopies.Should().Be(1);
        book.Deactivate();
        book.TryReserveCopy().Should().BeFalse();
        book.AvailableCopies.Should().Be(1);
    }

    [Fact]
    public void Update_stock_preserves_copies_already_on_loan()
    {
        var book = Book.Create("Dune", "ISBN-1", "Frank Herbert", totalCopies: 3);
        book.TryReserveCopy().Should().BeTrue();

        book.UpdateStock(totalCopies: 4);

        book.TotalCopies.Should().Be(4);
        book.AvailableCopies.Should().Be(3);
        var act = () => book.UpdateStock(totalCopies: 0);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Total copies cannot be less than the number of copies currently on loan.");
    }

    [Fact]
    public void Decrement_stock_rejects_removing_loaned_copies()
    {
        var book = Book.Create("Dune", "ISBN-1", "Frank Herbert", totalCopies: 1);
        book.TryReserveCopy().Should().BeTrue();

        var act = () => book.DecrementStock();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot remove copies that are currently on loan.");
        book.TotalCopies.Should().Be(1);
        book.AvailableCopies.Should().Be(0);
    }
}
