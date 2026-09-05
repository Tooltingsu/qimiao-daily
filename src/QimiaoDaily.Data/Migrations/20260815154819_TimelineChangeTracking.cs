using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Data;

#nullable disable

namespace QimiaoDaily.Data.Migrations;

[DbContext(typeof(QimiaoDailyDbContext))]
[Migration("20260815154819_TimelineChangeTracking")]
public partial class TimelineChangeTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CanonicalIdentity",
            table: "timeline_items",
            type: "TEXT",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ChangeKind",
            table: "timeline_items",
            type: "TEXT",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CanonicalIdentity", table: "timeline_items");
        migrationBuilder.DropColumn(name: "ChangeKind", table: "timeline_items");
    }
}
