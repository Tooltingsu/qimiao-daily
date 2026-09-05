using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QimiaoDaily.Data.Migrations
{
    /// <inheritdoc />
    public partial class StructuredGacha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GachaGroupKey",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GachaPoolKind",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GachaPoolPhase",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GachaGroupKey",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "GachaPoolKind",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "GachaPoolPhase",
                table: "timeline_items");
        }
    }
}
