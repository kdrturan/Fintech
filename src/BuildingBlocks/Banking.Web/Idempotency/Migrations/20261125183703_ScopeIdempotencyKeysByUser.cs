using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Web.Idempotency.Migrations
{
    /// <inheritdoc />
    public partial class ScopeIdempotencyKeysByUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_requests",
                schema: "idempotency",
                table: "requests");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                schema: "idempotency",
                table: "requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Only for the rows that exist already: nobody knows whose they are, so nobody can replay them. New rows must
            // say whose key it is, so the default does not stay.
            migrationBuilder.Sql("ALTER TABLE idempotency.requests ALTER COLUMN user_id DROP DEFAULT;");

            migrationBuilder.AddPrimaryKey(
                name: "pk_requests",
                schema: "idempotency",
                table: "requests",
                columns: new[] { "user_id", "key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_requests",
                schema: "idempotency",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "user_id",
                schema: "idempotency",
                table: "requests");

            migrationBuilder.AddPrimaryKey(
                name: "pk_requests",
                schema: "idempotency",
                table: "requests",
                column: "key");
        }
    }
}
