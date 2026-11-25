using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Modules.Transfers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferRequester : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "requested_by",
                schema: "transfers",
                table: "transfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Only for the rows that exist already: they have no known requester, so nobody can look them up. New rows must
            // say who asked, so the default does not stay.
            migrationBuilder.Sql("ALTER TABLE transfers.transfers ALTER COLUMN requested_by DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "requested_by",
                schema: "transfers",
                table: "transfers");
        }
    }
}
