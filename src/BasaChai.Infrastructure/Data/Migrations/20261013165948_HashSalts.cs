using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasaChai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HashSalts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hash_salts",
                columns: table => new
                {
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    salt = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hash_salts", x => x.period);
                });
            // A deleted salt must really go: vacuum this tiny table as soon as it has a dead row.
            migrationBuilder.Sql("ALTER TABLE hash_salts SET (autovacuum_vacuum_threshold = 0, autovacuum_vacuum_scale_factor = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hash_salts");
        }
    }
}
