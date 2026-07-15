using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialForecastStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    normalized_name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    latitude = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: false),
                    time_zone_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    location_type = table.Column<short>(type: "INTEGER", nullable: false),
                    coast_orientation_deg = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    is_preset = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "forecast_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    location_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider_code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    data_domain = table.Column<short>(type: "INTEGER", nullable: false),
                    endpoint_code = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    model_code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    fetched_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    range_start = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    range_end = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    quality_status = table.Column<short>(type: "INTEGER", nullable: false),
                    freshness = table.Column<short>(type: "INTEGER", nullable: false),
                    quality_flags = table.Column<int>(type: "INTEGER", nullable: false),
                    completeness = table.Column<double>(type: "REAL", nullable: false),
                    requested_latitude = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: false),
                    requested_longitude = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: false),
                    grid_latitude = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: true),
                    grid_longitude = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: true),
                    raw_payload_hash = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: true)
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
                name: "forecast_points",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    batch_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    forecast_time = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    wind_speed_ms = table.Column<double>(type: "REAL", nullable: true),
                    wind_gust_ms = table.Column<double>(type: "REAL", nullable: true),
                    wind_direction_deg = table.Column<double>(type: "REAL", nullable: true),
                    temperature_c = table.Column<double>(type: "REAL", nullable: true),
                    humidity_percent = table.Column<double>(type: "REAL", nullable: true),
                    pressure_hpa = table.Column<double>(type: "REAL", nullable: true),
                    cloud_cover_percent = table.Column<double>(type: "REAL", nullable: true),
                    precipitation_mm = table.Column<double>(type: "REAL", nullable: true),
                    cape_jkg = table.Column<double>(type: "REAL", nullable: true),
                    visibility_m = table.Column<double>(type: "REAL", nullable: true),
                    weather_code = table.Column<int>(type: "INTEGER", nullable: true),
                    thunderstorm = table.Column<bool>(type: "INTEGER", nullable: true),
                    wave_height_m = table.Column<double>(type: "REAL", nullable: true),
                    wave_period_s = table.Column<double>(type: "REAL", nullable: true),
                    wave_peak_period_s = table.Column<double>(type: "REAL", nullable: true),
                    wave_direction_deg = table.Column<double>(type: "REAL", nullable: true),
                    wind_wave_height_m = table.Column<double>(type: "REAL", nullable: true),
                    wind_wave_period_s = table.Column<double>(type: "REAL", nullable: true),
                    wind_wave_peak_period_s = table.Column<double>(type: "REAL", nullable: true),
                    wind_wave_direction_deg = table.Column<double>(type: "REAL", nullable: true),
                    swell_height_m = table.Column<double>(type: "REAL", nullable: true),
                    swell_period_s = table.Column<double>(type: "REAL", nullable: true),
                    swell_peak_period_s = table.Column<double>(type: "REAL", nullable: true),
                    swell_direction_deg = table.Column<double>(type: "REAL", nullable: true),
                    sea_temperature_c = table.Column<double>(type: "REAL", nullable: true),
                    current_speed_ms = table.Column<double>(type: "REAL", nullable: true),
                    current_direction_deg = table.Column<double>(type: "REAL", nullable: true),
                    tide_height_m = table.Column<double>(type: "REAL", nullable: true),
                    tide_type = table.Column<short>(type: "INTEGER", nullable: true),
                    quality_status = table.Column<short>(type: "INTEGER", nullable: false),
                    freshness = table.Column<short>(type: "INTEGER", nullable: false),
                    missing_mask = table.Column<long>(type: "INTEGER", nullable: false),
                    quality_flags = table.Column<int>(type: "INTEGER", nullable: false),
                    completeness = table.Column<double>(type: "REAL", nullable: false)
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
                    forecast_point_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    metric = table.Column<short>(type: "INTEGER", nullable: false),
                    provider_code = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    source_model = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    batch_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    forecast_time = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    quality_status = table.Column<short>(type: "INTEGER", nullable: false),
                    freshness = table.Column<short>(type: "INTEGER", nullable: false),
                    quality_flags = table.Column<int>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_forecast_batches_location_id_fetched_at",
                table: "forecast_batches",
                columns: new[] { "location_id", "fetched_at" });

            migrationBuilder.CreateIndex(
                name: "IX_forecast_batches_provider_code_data_domain_model_code_location_id_issued_at_range_start",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_point_sources");

            migrationBuilder.DropTable(
                name: "forecast_points");

            migrationBuilder.DropTable(
                name: "forecast_batches");

            migrationBuilder.DropTable(
                name: "locations");
        }
    }
}
