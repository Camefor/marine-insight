using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MarineInsight.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    location_type = table.Column<short>(type: "smallint", nullable: false),
                    coast_orientation_deg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    is_preset = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "forecast_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    data_domain = table.Column<short>(type: "smallint", nullable: false),
                    endpoint_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    model_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    range_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    range_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    quality_status = table.Column<short>(type: "smallint", nullable: false),
                    freshness = table.Column<short>(type: "smallint", nullable: false),
                    quality_flags = table.Column<int>(type: "integer", nullable: false),
                    completeness = table.Column<double>(type: "double precision", nullable: false),
                    requested_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    requested_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    grid_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    grid_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    raw_payload_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_forecast_batches_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_claims_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "favorite_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultActivity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    ForecastFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Hours = table.Column<int>(type: "integer", nullable: false),
                    Activities = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_claims_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_user_logins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_settings",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WindSpeedUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WaveHeightUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TemperatureUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultActivity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_user_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "forecast_points",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    wind_speed_ms = table.Column<double>(type: "double precision", nullable: true),
                    wind_gust_ms = table.Column<double>(type: "double precision", nullable: true),
                    wind_direction_deg = table.Column<double>(type: "double precision", nullable: true),
                    temperature_c = table.Column<double>(type: "double precision", nullable: true),
                    humidity_percent = table.Column<double>(type: "double precision", nullable: true),
                    pressure_hpa = table.Column<double>(type: "double precision", nullable: true),
                    cloud_cover_percent = table.Column<double>(type: "double precision", nullable: true),
                    precipitation_mm = table.Column<double>(type: "double precision", nullable: true),
                    cape_jkg = table.Column<double>(type: "double precision", nullable: true),
                    visibility_m = table.Column<double>(type: "double precision", nullable: true),
                    weather_code = table.Column<int>(type: "integer", nullable: true),
                    thunderstorm = table.Column<bool>(type: "boolean", nullable: true),
                    wave_height_m = table.Column<double>(type: "double precision", nullable: true),
                    wave_period_s = table.Column<double>(type: "double precision", nullable: true),
                    wave_peak_period_s = table.Column<double>(type: "double precision", nullable: true),
                    wave_direction_deg = table.Column<double>(type: "double precision", nullable: true),
                    wind_wave_height_m = table.Column<double>(type: "double precision", nullable: true),
                    wind_wave_period_s = table.Column<double>(type: "double precision", nullable: true),
                    wind_wave_peak_period_s = table.Column<double>(type: "double precision", nullable: true),
                    wind_wave_direction_deg = table.Column<double>(type: "double precision", nullable: true),
                    swell_height_m = table.Column<double>(type: "double precision", nullable: true),
                    swell_period_s = table.Column<double>(type: "double precision", nullable: true),
                    swell_peak_period_s = table.Column<double>(type: "double precision", nullable: true),
                    swell_direction_deg = table.Column<double>(type: "double precision", nullable: true),
                    sea_temperature_c = table.Column<double>(type: "double precision", nullable: true),
                    current_speed_ms = table.Column<double>(type: "double precision", nullable: true),
                    current_direction_deg = table.Column<double>(type: "double precision", nullable: true),
                    tide_height_m = table.Column<double>(type: "double precision", nullable: true),
                    tide_type = table.Column<short>(type: "smallint", nullable: true),
                    quality_status = table.Column<short>(type: "smallint", nullable: false),
                    freshness = table.Column<short>(type: "smallint", nullable: false),
                    missing_mask = table.Column<long>(type: "bigint", nullable: false),
                    quality_flags = table.Column<int>(type: "integer", nullable: false),
                    completeness = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_points", x => x.id);
                    table.ForeignKey(
                        name: "FK_forecast_points_forecast_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "forecast_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "forecast_point_sources",
                columns: table => new
                {
                    forecast_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric = table.Column<short>(type: "smallint", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_model = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    quality_status = table.Column<short>(type: "smallint", nullable: false),
                    freshness = table.Column<short>(type: "smallint", nullable: false),
                    quality_flags = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_point_sources", x => new { x.forecast_point_id, x.metric });
                    table.ForeignKey(
                        name: "FK_forecast_point_sources_forecast_points_forecast_point_id",
                        column: x => x.forecast_point_id,
                        principalTable: "forecast_points",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "locations",
                columns: new[] { "id", "coast_orientation_deg", "created_at", "display_name", "is_preset", "latitude", "location_type", "longitude", "normalized_name", "time_zone_id" },
                values: new object[,]
                {
                    { new Guid("70cfb8c4-7af7-4c43-8f38-9a27e7cc2de7"), null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "嵊泗列岛", true, 30.727m, (short)1, 122.451m, "嵊泗列岛", "Asia/Shanghai" },
                    { new Guid("8a477d67-73fa-4f43-b954-cd29d238a89d"), null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "东极岛", true, 30.194m, (short)1, 122.687m, "东极岛", "Asia/Shanghai" },
                    { new Guid("d6ac8e90-44ae-4d1f-88b9-8b73db7af6a1"), null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "普陀山", true, 30.010m, (short)1, 122.388m, "普陀山", "Asia/Shanghai" }
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
                name: "IX_forecast_batches_location_id_fetched_at",
                table: "forecast_batches",
                columns: new[] { "location_id", "fetched_at" });

            migrationBuilder.CreateIndex(
                name: "IX_forecast_batches_provider_code_data_domain_model_code_locat~",
                table: "forecast_batches",
                columns: new[] { "provider_code", "data_domain", "model_code", "location_id", "issued_at", "range_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_forecast_points_batch_id_forecast_time",
                table: "forecast_points",
                columns: new[] { "batch_id", "forecast_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_normalized_name_latitude_longitude",
                table: "locations",
                columns: new[] { "normalized_name", "latitude", "longitude" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_query_history_UserId_CreatedAtUtc",
                table: "query_history",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_role_claims_RoleId",
                table: "role_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_claims_UserId",
                table: "user_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_logins_UserId",
                table: "user_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "favorite_locations");

            migrationBuilder.DropTable(
                name: "forecast_point_sources");

            migrationBuilder.DropTable(
                name: "query_history");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_settings");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "forecast_points");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "forecast_batches");

            migrationBuilder.DropTable(
                name: "locations");
        }
    }
}
