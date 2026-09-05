using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QimiaoDaily.Data.Migrations
{
    /// <inheritdoc />
    public partial class ArtworkDailyRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artwork_daily_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TargetCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FetchedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NewCandidateCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artwork_daily_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artwork_daily_runs_CompletedAt",
                table: "artwork_daily_runs",
                column: "CompletedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artwork_daily_runs");
        }
    }
}
