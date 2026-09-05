using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QimiaoDaily.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManualDataPivot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataOrigin",
                table: "timeline_items",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "AutoCollected");

            migrationBuilder.AddColumn<bool>(
                name: "UserConfirmed",
                table: "timeline_items",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DataOrigin",
                table: "birthdays",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "AutoCollected");

            migrationBuilder.AddColumn<string>(
                name: "OriginTrace",
                table: "birthdays",
                type: "TEXT",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "UserConfirmed",
                table: "birthdays",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "banners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Game = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CustomType = table.Column<string>(type: "TEXT", nullable: true),
                    StartAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UserConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Archived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "endgame_anchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endgame_anchors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "endgame_occurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StartAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsOverride = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endgame_occurrences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "endgame_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Game = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RuleKind = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endgame_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Game = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    VersionNumber = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    VersionName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UserConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Archived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manual_data_audits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_data_audits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "manual_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Game = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UserConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Archived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "banner_characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BannerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banner_characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_banner_characters_banners_BannerId",
                        column: x => x.BannerId,
                        principalTable: "banners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_banner_characters_BannerId_SortOrder",
                table: "banner_characters",
                columns: new[] { "BannerId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_banners_Game_StartAt",
                table: "banners",
                columns: new[] { "Game", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_endgame_anchors_RuleId",
                table: "endgame_anchors",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_endgame_occurrences_RuleId_StartAt",
                table: "endgame_occurrences",
                columns: new[] { "RuleId", "StartAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_endgame_rules_Game_Name",
                table: "endgame_rules",
                columns: new[] { "Game", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_versions_Game_VersionNumber",
                table: "game_versions",
                columns: new[] { "Game", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_data_audits_EntityType_EntityId_OccurredAt",
                table: "manual_data_audits",
                columns: new[] { "EntityType", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_events_Game_StartAt",
                table: "manual_events",
                columns: new[] { "Game", "StartAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "banner_characters");

            migrationBuilder.DropTable(
                name: "endgame_anchors");

            migrationBuilder.DropTable(
                name: "endgame_occurrences");

            migrationBuilder.DropTable(
                name: "endgame_rules");

            migrationBuilder.DropTable(
                name: "game_versions");

            migrationBuilder.DropTable(
                name: "manual_data_audits");

            migrationBuilder.DropTable(
                name: "manual_events");

            migrationBuilder.DropTable(
                name: "banners");

            migrationBuilder.DropColumn(
                name: "DataOrigin",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "UserConfirmed",
                table: "timeline_items");

            migrationBuilder.DropColumn(
                name: "DataOrigin",
                table: "birthdays");

            migrationBuilder.DropColumn(
                name: "OriginTrace",
                table: "birthdays");

            migrationBuilder.DropColumn(
                name: "UserConfirmed",
                table: "birthdays");
        }
    }
}
