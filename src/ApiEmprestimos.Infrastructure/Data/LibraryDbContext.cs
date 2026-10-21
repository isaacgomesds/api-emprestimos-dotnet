using ApiEmprestimos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiEmprestimos.Infrastructure.Data;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<IdempotentRequest> IdempotentRequests => Set<IdempotentRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("Books");
            entity.HasKey(book => book.Id);
            entity.Property(book => book.Title).IsRequired().HasMaxLength(250);
            entity.Property(book => book.Isbn).IsRequired().HasMaxLength(20);
            entity.Property(book => book.Author).IsRequired().HasMaxLength(200);
            entity.HasIndex(book => book.Isbn).IsUnique();
            entity.HasData(
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Title = "The Hobbit",
                    Isbn = "9780547928227",
                    Author = "J.R.R. Tolkien",
                    TotalCopies = 4,
                    AvailableCopies = 4,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111112"),
                    Title = "Pride and Prejudice",
                    Isbn = "9780141439518",
                    Author = "Jane Austen",
                    TotalCopies = 3,
                    AvailableCopies = 3,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111113"),
                    Title = "Nineteen Eighty-Four",
                    Isbn = "9780451524935",
                    Author = "George Orwell",
                    TotalCopies = 5,
                    AvailableCopies = 5,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111114"),
                    Title = "The Great Gatsby",
                    Isbn = "9780743273565",
                    Author = "F. Scott Fitzgerald",
                    TotalCopies = 3,
                    AvailableCopies = 3,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111115"),
                    Title = "To Kill a Mockingbird",
                    Isbn = "9780061120084",
                    Author = "Harper Lee",
                    TotalCopies = 4,
                    AvailableCopies = 4,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111116"),
                    Title = "Dune",
                    Isbn = "9780441172719",
                    Author = "Frank Herbert",
                    TotalCopies = 2,
                    AvailableCopies = 2,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111117"),
                    Title = "The Catcher in the Rye",
                    Isbn = "9780316769488",
                    Author = "J.D. Salinger",
                    TotalCopies = 3,
                    AvailableCopies = 3,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111118"),
                    Title = "One Hundred Years of Solitude",
                    Isbn = "9780060883287",
                    Author = "Gabriel Garcia Marquez",
                    TotalCopies = 2,
                    AvailableCopies = 2,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111119"),
                    Title = "The Name of the Rose",
                    Isbn = "9780156001311",
                    Author = "Umberto Eco",
                    TotalCopies = 2,
                    AvailableCopies = 2,
                    IsActive = true
                },
                new
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111120"),
                    Title = "The Fellowship of the Ring",
                    Isbn = "9780547928210",
                    Author = "J.R.R. Tolkien",
                    TotalCopies = 4,
                    AvailableCopies = 4,
                    IsActive = true
                });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).IsRequired().HasMaxLength(200);
            entity.Property(user => user.Email).IsRequired().HasMaxLength(320);
            entity.HasIndex(user => user.Email);
            entity.HasData(
                new
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Standard User",
                    Email = "standard.user@example.com",
                    CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222223"),
                    Name = "Administrator",
                    Email = "admin@example.com",
                    CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222224"),
                    Name = "Librarian",
                    Email = "librarian@example.com",
                    CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.ToTable("Loans");
            entity.HasKey(loan => loan.Id);
            entity.Property(loan => loan.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(loan => loan.Book)
                .WithMany()
                .HasForeignKey(loan => loan.BookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(loan => loan.User)
                .WithMany()
                .HasForeignKey(loan => loan.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(auditEvent => auditEvent.Id);
            entity.Property(auditEvent => auditEvent.EntityName).IsRequired().HasMaxLength(100);
            entity.Property(auditEvent => auditEvent.EntityId).IsRequired().HasMaxLength(100);
            entity.Property(auditEvent => auditEvent.Action).IsRequired().HasMaxLength(100);
            entity.Property(auditEvent => auditEvent.Actor).IsRequired().HasMaxLength(200);
            entity.Property(auditEvent => auditEvent.CorrelationId).IsRequired().HasMaxLength(100);
            entity.Property(auditEvent => auditEvent.DetailsJson).IsRequired().HasColumnType("jsonb");
            entity.HasIndex(auditEvent => new { auditEvent.CorrelationId, auditEvent.TimestampUtc });
        });

        modelBuilder.Entity<IdempotentRequest>(entity =>
        {
            entity.ToTable("IdempotentRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Key).IsRequired().HasMaxLength(200);
            entity.Property(request => request.RequestHash).IsRequired().HasMaxLength(64);
            entity.Property(request => request.ResponseBody).IsRequired().HasColumnType("text");
            entity.HasIndex(request => request.Key).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
