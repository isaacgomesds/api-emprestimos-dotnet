namespace ApiEmprestimos.Application.Common.DTOs;

public sealed record UserResponseDto(Guid Id, string Name, string Email, DateTime CreatedAtUtc);
