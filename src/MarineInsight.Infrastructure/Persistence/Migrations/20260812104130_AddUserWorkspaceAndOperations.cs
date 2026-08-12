using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserWorkspaceAndOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "favorite_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DefaultActivity = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_favorite_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_favorite_locations_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_favorite_locations_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "query_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    ForecastFromUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Hours = table.Column<int>(type: "INTEGER", nullable: false),
                    Activities = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    AnalysisId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_query_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_query_history_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_settings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WindSpeedUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    WaveHeightUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TemperatureUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DefaultActivity = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_settings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_user_settings_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_ActorUserId_CreatedAtUtc",
                table: "audit_logs",
                columns: new[] { "ActorUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_CreatedAtUtc",
                table: "audit_logs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_favorite_locations_LocationId",
                table: "favorite_locations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_favorite_locations_UserId_LocationId",
                table: "favorite_locations",
                columns: new[] { "UserId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_favorite_locations_UserId_SortOrder",
                table: "favorite_locations",
                columns: new[] { "UserId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_query_history_UserId_CreatedAtUtc",
                table: "query_history",
                columns: new[] { "UserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "favorite_locations");

            migrationBuilder.DropTable(
                name: "query_history");

            migrationBuilder.DropTable(
                name: "user_settings");
        }
    }
}
