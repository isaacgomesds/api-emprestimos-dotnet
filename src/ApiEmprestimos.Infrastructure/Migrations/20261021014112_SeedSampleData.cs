using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ApiEmprestimos.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Books",
                columns: new[] { "Id", "Author", "AvailableCopies", "IsActive", "Isbn", "Title", "TotalCopies" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "J.R.R. Tolkien", 4, true, "9780547928227", "The Hobbit", 4 },
                    { new Guid("11111111-1111-1111-1111-111111111112"), "Jane Austen", 3, true, "9780141439518", "Pride and Prejudice", 3 },
                    { new Guid("11111111-1111-1111-1111-111111111113"), "George Orwell", 5, true, "9780451524935", "Nineteen Eighty-Four", 5 },
                    { new Guid("11111111-1111-1111-1111-111111111114"), "F. Scott Fitzgerald", 3, true, "9780743273565", "The Great Gatsby", 3 },
                    { new Guid("11111111-1111-1111-1111-111111111115"), "Harper Lee", 4, true, "9780061120084", "To Kill a Mockingbird", 4 },
                    { new Guid("11111111-1111-1111-1111-111111111116"), "Frank Herbert", 2, true, "9780441172719", "Dune", 2 },
                    { new Guid("11111111-1111-1111-1111-111111111117"), "J.D. Salinger", 3, true, "9780316769488", "The Catcher in the Rye", 3 },
                    { new Guid("11111111-1111-1111-1111-111111111118"), "Gabriel Garcia Marquez", 2, true, "9780060883287", "One Hundred Years of Solitude", 2 },
                    { new Guid("11111111-1111-1111-1111-111111111119"), "Umberto Eco", 2, true, "9780156001311", "The Name of the Rose", 2 },
                    { new Guid("11111111-1111-1111-1111-111111111120"), "J.R.R. Tolkien", 4, true, "9780547928210", "The Fellowship of the Ring", 4 }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAtUtc", "Email", "Name" },
                values: new object[,]
                {
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "standard.user@example.com", "Standard User" },
                    { new Guid("22222222-2222-2222-2222-222222222223"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@example.com", "Administrator" },
                    { new Guid("22222222-2222-2222-2222-222222222224"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "librarian@example.com", "Librarian" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111112"));

            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111113"));

            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111114"));

            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111115"));

            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111116"));

            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111117"));

            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111118"));

            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111119"));

            migrationBuilder.DeleteData(
                table: "Books",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111120"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222223"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222224"));
        }
    }
}
