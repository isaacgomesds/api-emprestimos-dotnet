using ApiEmprestimos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiEmprestimos.Infrastructure.Migrations;

[DbContext(typeof(LibraryDbContext))]
[Migration("20261021030000_AddIdempotentRequestHash")]
public partial class AddIdempotentRequestHash : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RequestHash",
            table: "IdempotentRequests",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.Sql(
            """UPDATE "IdempotentRequests" SET "RequestHash" = '' WHERE "RequestHash" IS NULL""");

        migrationBuilder.AlterColumn<string>(
            name: "RequestHash",
            table: "IdempotentRequests",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(64)",
            oldMaxLength: 64,
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RequestHash",
            table: "IdempotentRequests");
    }
}
