namespace ApiEmprestimos.Application.Common.DTOs;

/// <summary>Represents a book returned by the application.</summary>
public sealed record BookResponseDto(
    Guid Id,
    string Title,
    string Isbn,
    string Author,
    int TotalCopies,
    int AvailableCopies,
    bool IsActive);
