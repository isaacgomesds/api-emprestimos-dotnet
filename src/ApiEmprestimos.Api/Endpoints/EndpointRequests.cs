using System.ComponentModel.DataAnnotations;

namespace ApiEmprestimos.Api.Endpoints;

/// <summary>Request payload for creating a book.</summary>
public sealed record CreateBookRequest
{
    [Required, MaxLength(250)]
    public required string Title { get; init; }

    [Required, MaxLength(20)]
    public required string Isbn { get; init; }

    [Required, MaxLength(200)]
    public required string Author { get; init; }

    [Range(0, int.MaxValue)]
    public int TotalCopies { get; init; }
}

/// <summary>Request payload for partially updating a book.</summary>
public sealed record UpdateBookRequest
{
    [MaxLength(250)]
    public string? Title { get; init; }

    [MaxLength(200)]
    public string? Author { get; init; }

    [Range(0, int.MaxValue)]
    public int? TotalCopies { get; init; }
}

/// <summary>Request payload for creating a library user.</summary>
public sealed record CreateUserRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }
}

/// <summary>Request payload for creating a loan.</summary>
public sealed record CreateLoanRequest
{
    [Required]
    public Guid BookId { get; init; }

    [Required]
    public Guid UserId { get; init; }

    public DateTimeOffset LoanDateUtc { get; init; }

    public DateTimeOffset DueDateUtc { get; init; }
}
