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
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).IsRequired().HasMaxLength(200);
            entity.Property(user => user.Email).IsRequired().HasMaxLength(320);
            entity.HasIndex(user => user.Email);
        });

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.ToTable("Loans");
            entity.HasKey(loan => loan.Id);
            entity.Property(loan => loan.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne<Book>()
                .WithMany()
                .HasForeignKey(loan => loan.BookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>()
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
            entity.Property(request => request.ResponseBody).IsRequired().HasColumnType("text");
            entity.HasIndex(request => request.Key).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
