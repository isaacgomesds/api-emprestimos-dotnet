namespace ApiEmprestimos.Application.Common.DTOs;

/// <summary>Represents a library user returned by the application.</summary>
public sealed record UserResponseDto(Guid Id, string Name, string Email, DateTimeOffset CreatedAtUtc);
