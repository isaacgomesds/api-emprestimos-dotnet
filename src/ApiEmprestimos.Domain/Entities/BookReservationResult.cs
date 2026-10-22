namespace ApiEmprestimos.Domain.Entities;

public enum BookReservationResult
{
    Reserved,
    BookNotFound,
    BookInactive,
    NoAvailableCopies
}
