using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    range_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    range_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hours = table.Column<int>(type: "integer", nullable: false),
                    algorithm_version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    source_set_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    activity_type = table.Column<short>(type: "smallint", nullable: true),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    risk_level = table.Column<short>(type: "smallint", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    recommended_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recommended_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    return_before = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    summary_template_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_analysis_results_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_risks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rule_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    severity = table.Column<short>(type: "smallint", nullable: false),
                    actual = table.Column<double>(type: "double precision", nullable: true),
                    threshold = table.Column<double>(type: "double precision", nullable: true),
                    penalty = table.Column<double>(type: "double precision", nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_risks", x => x.id);
                    table.ForeignKey(
                        name: "FK_analysis_risks_analysis_results_analysis_result_id",
                        column: x => x.analysis_result_id,
                        principalTable: "analysis_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_source_batches",
                columns: table => new
                {
                    analysis_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_role = table.Column<short>(type: "smallint", nullable: false),
                    data_domain = table.Column<short>(type: "smallint", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_model = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    selection_policy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_source_batches", x => new { x.analysis_result_id, x.batch_id, x.source_role });
                    table.ForeignKey(
                        name: "FK_analysis_source_batches_analysis_results_analysis_result_id",
                        column: x => x.analysis_result_id,
                        principalTable: "analysis_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_results_source_set_hash",
                table: "analysis_results",
                column: "source_set_hash");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_results_user_id_created_at",
                table: "analysis_results",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_risks_analysis_result_id_severity",
                table: "analysis_risks",
                columns: new[] { "analysis_result_id", "severity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_risks");

            migrationBuilder.DropTable(
                name: "analysis_source_batches");

            migrationBuilder.DropTable(
                name: "analysis_results");
        }
    }
}
