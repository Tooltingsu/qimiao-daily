using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QimiaoDaily.Data.Migrations
{
    /// <inheritdoc />
    public partial class TimelineTimeSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndExpression",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndTimeEvidenceKey",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndTimePrecision",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EndTimeSource",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartExpression",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartTimeEvidenceKey",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartTimePrecision",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StartTimeSource",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndExpression",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "EndTimeEvidenceKey",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "EndTimePrecision",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "EndTimeSource",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "StartExpression",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "StartTimeEvidenceKey",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "StartTimePrecision",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "StartTimeSource",
                table: "timeline_items");
        }
    }
}
