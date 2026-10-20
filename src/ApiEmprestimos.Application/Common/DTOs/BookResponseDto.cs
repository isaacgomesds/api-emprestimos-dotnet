namespace ApiEmprestimos.Application.Common.DTOs;

public sealed record BookResponseDto(
    Guid Id,
    string Title,
    string Isbn,
    string Author,
    int TotalCopies,
    int AvailableCopies,
    bool IsActive);
