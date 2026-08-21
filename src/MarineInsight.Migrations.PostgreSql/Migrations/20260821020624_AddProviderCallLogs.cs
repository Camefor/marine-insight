using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCallLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_call_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: true),
                    CredentialHint = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LatitudeBucket = table.Column<double>(type: "double precision", nullable: true),
                    LongitudeBucket = table.Column<double>(type: "double precision", nullable: true),
                    RangeStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RangeEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedDays = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    CreditsUsed = table.Column<int>(type: "integer", nullable: true),
                    RemainingCredits = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_call_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_logs_ActorUserId_StartedAtUtc",
                table: "provider_call_logs",
                columns: new[] { "ActorUserId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_logs_Outcome_StartedAtUtc",
                table: "provider_call_logs",
                columns: new[] { "Outcome", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_logs_ProviderCode_StartedAtUtc",
                table: "provider_call_logs",
                columns: new[] { "ProviderCode", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_logs_StartedAtUtc",
                table: "provider_call_logs",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_call_logs");
        }
    }
}
