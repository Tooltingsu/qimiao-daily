using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QimiaoDaily.Data.Migrations
{
    /// <inheritdoc />
    public partial class V3PersistenceAdapters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RuleKey",
                table: "endgame_rules",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "endgame_rules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimePrecision",
                table: "endgame_rules",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "EXACT");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GeneratedAt",
                table: "endgame_occurrences",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "endgame_occurrences",
                type: "TEXT",
                maxLength: 4000,
                nullable: false,
                defaultValue: "EXACT");

            migrationBuilder.AddColumn<DateOnly>(
                name: "OccurrenceDate",
                table: "endgame_occurrences",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduledDate",
                table: "endgame_occurrences",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "endgame_occurrences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "endgame_occurrences",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimePrecision",
                table: "endgame_occurrences",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "AutoCollected");

            migrationBuilder.AddColumn<DateOnly>(
                name: "AnchorDate",
                table: "endgame_anchors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataOrigin",
                table: "anniversaries",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "anniversaries",
                type: "TEXT",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "UserConfirmed",
                table: "anniversaries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE endgame_rules SET RuleKey = 'LEGACY-' || Id WHERE RuleKey = '';" );
            migrationBuilder.Sql("UPDATE endgame_rules SET TimePrecision = CASE WHEN lower(ConfigurationJson) LIKE '%date_only%' OR lower(ConfigurationJson) LIKE '%dateonly%' THEN 'DATE_ONLY' ELSE 'EXACT' END WHERE TimePrecision = '';" );
            migrationBuilder.Sql("UPDATE endgame_occurrences SET ScheduledDate = date(StartAt), OccurrenceDate = date(StartAt), TimePrecision = CASE WHEN EXISTS (SELECT 1 FROM endgame_rules r WHERE r.Id = endgame_occurrences.RuleId AND r.TimePrecision = 'DATE_ONLY') THEN 'DATE_ONLY' ELSE 'EXACT' END WHERE ScheduledDate IS NULL;" );
            migrationBuilder.Sql("UPDATE endgame_anchors SET AnchorDate = date(StartsAt) WHERE AnchorDate IS NULL;" );

            migrationBuilder.CreateTable(
                name: "qimiao_import_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    RecordId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    NaturalKey = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    FormalEntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qimiao_import_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_endgame_rules_RuleKey",
                table: "endgame_rules",
                column: "RuleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_endgame_occurrences_RuleId_Sequence",
                table: "endgame_occurrences",
                columns: new[] { "RuleId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qimiao_import_records_RecordType_NaturalKey",
                table: "qimiao_import_records",
                columns: new[] { "RecordType", "NaturalKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qimiao_import_records_RecordType_RecordId",
                table: "qimiao_import_records",
                columns: new[] { "RecordType", "RecordId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qimiao_import_records");

            migrationBuilder.DropIndex(
                name: "IX_endgame_rules_RuleKey",
                table: "endgame_rules");

            migrationBuilder.DropIndex(
                name: "IX_endgame_occurrences_RuleId_Sequence",
                table: "endgame_occurrences");

            migrationBuilder.DropColumn(
                name: "RuleKey",
                table: "endgame_rules");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "endgame_rules");

            migrationBuilder.DropColumn(
                name: "TimePrecision",
                table: "endgame_rules");

            migrationBuilder.DropColumn(
                name: "GeneratedAt",
                table: "endgame_occurrences");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "endgame_occurrences");

            migrationBuilder.DropColumn(
                name: "OccurrenceDate",
                table: "endgame_occurrences");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "endgame_occurrences");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "endgame_occurrences");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "endgame_occurrences");

            migrationBuilder.DropColumn(
                name: "TimePrecision",
                table: "endgame_occurrences");

            migrationBuilder.DropColumn(
                name: "AnchorDate",
                table: "endgame_anchors");

            migrationBuilder.DropColumn(
                name: "DataOrigin",
                table: "anniversaries");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "anniversaries");

            migrationBuilder.DropColumn(
                name: "UserConfirmed",
                table: "anniversaries");
        }
    }
}
