using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Web.Idempotency.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "idempotency");

            migrationBuilder.CreateTable(
                name: "requests",
                schema: "idempotency",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requests", x => x.key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "requests",
                schema: "idempotency");
        }
    }
}
