namespace ApiEmprestimos.Domain.Entities;

public sealed class Book
{
    private Book()
    {
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Isbn { get; private set; } = string.Empty;
    public string Author { get; private set; } = string.Empty;
    public int TotalCopies { get; private set; }
    public int AvailableCopies { get; private set; }
    public bool IsActive { get; private set; }

    public static Book Create(string title, string isbn, string author, int totalCopies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);

        if (totalCopies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCopies), "Total copies cannot be negative.");
        }

        return new Book
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Isbn = isbn.Trim(),
            Author = author.Trim(),
            TotalCopies = totalCopies,
            AvailableCopies = totalCopies,
            IsActive = true
        };
    }

    public void UpdateDetails(string title, string author)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);

        Title = title.Trim();
        Author = author.Trim();
    }

    public void UpdateStock(int totalCopies)
    {
        if (totalCopies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCopies), "Total copies cannot be negative.");
        }

        var loanedCopies = TotalCopies - AvailableCopies;
        if (totalCopies < loanedCopies)
        {
            throw new InvalidOperationException("Total copies cannot be less than the number of copies currently on loan.");
        }

        TotalCopies = totalCopies;
        AvailableCopies = totalCopies - loanedCopies;
    }

    public void IncrementStock(int quantity = 1)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        TotalCopies = checked(TotalCopies + quantity);
        AvailableCopies = checked(AvailableCopies + quantity);
    }

    public void DecrementStock(int quantity = 1)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (quantity > AvailableCopies)
        {
            throw new InvalidOperationException("Cannot remove copies that are currently on loan.");
        }

        TotalCopies -= quantity;
        AvailableCopies -= quantity;
    }

    public bool TryReserveCopy()
    {
        if (!IsActive)
        {
            return false;
        }

        if (AvailableCopies <= 0)
        {
            return false;
        }

        AvailableCopies -= 1;
        return true;
    }

    public void ReleaseCopy()
    {
        if (AvailableCopies >= TotalCopies)
        {
            throw new InvalidOperationException("Cannot release a copy when all copies are already available.");
        }

        AvailableCopies += 1;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
