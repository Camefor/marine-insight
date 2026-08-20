using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider_name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    key_hint = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    encrypted_value = table.Column<string>(type: "TEXT", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    health = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    remaining_credits = table.Column<int>(type: "INTEGER", nullable: true),
                    credit_warning = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_checked_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    last_failure_reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_credentials", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_provider_credentials_provider_name",
                table: "provider_credentials",
                column: "provider_name",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_provider_credentials_provider_name_key_hint",
                table: "provider_credentials",
                columns: new[] { "provider_name", "key_hint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_credentials");
        }
    }
}
