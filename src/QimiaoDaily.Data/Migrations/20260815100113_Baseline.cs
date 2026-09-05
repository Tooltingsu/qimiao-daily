using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QimiaoDaily.Data.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anniversaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anniversaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "artworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    ArtworkId = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Author = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailSha256 = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReviewStatus = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedForReport = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "birthdays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Character = table.Column<string>(type: "TEXT", nullable: false),
                    Franchise = table.Column<string>(type: "TEXT", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Evidence = table.Column<string>(type: "TEXT", nullable: false),
                    VerificationStatus = table.Column<string>(type: "TEXT", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_birthdays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "endgame_cycle_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameCode = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RuleVersion = table.Column<string>(type: "TEXT", nullable: false),
                    TimelineItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    VerificationStatus = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewStatus = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endgame_cycle_instances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "endgame_cycle_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameCode = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    RecurrenceKind = table.Column<string>(type: "TEXT", nullable: false),
                    IntervalDays = table.Column<int>(type: "INTEGER", nullable: true),
                    AnchorStart = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RuleVersion = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Evidence = table.Column<string>(type: "TEXT", nullable: false),
                    VerificationStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endgame_cycle_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "git_commit_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Repository = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Sha = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    Author = table.Column<string>(type: "TEXT", nullable: true),
                    AuthorDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CommitterDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PullRequestNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    PullRequestUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SelectedForReport = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_git_commit_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "legacy_archive_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TableName = table.Column<string>(type: "TEXT", nullable: false),
                    RowKey = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_archive_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "legacy_import_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourcePath = table.Column<string>(type: "TEXT", nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BackupPath = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TimelineItemsImported = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchivedRows = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_import_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provider_health_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastFailureAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastLatencyMs = table.Column<long>(type: "INTEGER", nullable: false),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ParserStatus = table.Column<string>(type: "TEXT", nullable: false),
                    FailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_health_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "report_drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_drafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "review_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TimelineItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scheduler_task_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    ScheduleText = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxRetries = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduler_task_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "seen_artworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    ArtworkId = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ContentSha256 = table.Column<string>(type: "TEXT", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seen_artworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "timeline_item_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TimelineItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: false),
                    NewValue = table.Column<string>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timeline_item_revisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "timeline_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameCode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReviewStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    VerificationStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SourceTime = table.Column<string>(type: "TEXT", nullable: true),
                    SourceTimezone = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    TimePrecision = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timeline_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "report_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReportDraftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Dirty = table.Column<bool>(type: "INTEGER", nullable: false),
                    ManualOverride = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_report_sections_report_drafts_ReportDraftId",
                        column: x => x.ReportDraftId,
                        principalTable: "report_drafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TimelineItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceProvider = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    PageTitle = table.Column<string>(type: "TEXT", nullable: true),
                    SourceText = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    OriginalTimezone = table.Column<string>(type: "TEXT", nullable: true),
                    NormalizedTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ParserVersion = table.Column<string>(type: "TEXT", nullable: false),
                    VerificationStatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evidence_timeline_items_TimelineItemId",
                        column: x => x.TimelineItemId,
                        principalTable: "timeline_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artworks_NormalizedUrl",
                table: "artworks",
                column: "NormalizedUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_artworks_Platform_ArtworkId",
                table: "artworks",
                columns: new[] { "Platform", "ArtworkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_birthdays_Character_Franchise",
                table: "birthdays",
                columns: new[] { "Character", "Franchise" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_events_EventDate_Title",
                table: "calendar_events",
                columns: new[] { "EventDate", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_endgame_cycle_instances_GameCode_CanonicalName_StartAt",
                table: "endgame_cycle_instances",
                columns: new[] { "GameCode", "CanonicalName", "StartAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_endgame_cycle_instances_TimelineItemId",
                table: "endgame_cycle_instances",
                column: "TimelineItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_endgame_cycle_rules_GameCode_CanonicalName_RuleVersion",
                table: "endgame_cycle_rules",
                columns: new[] { "GameCode", "CanonicalName", "RuleVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_TimelineItemId",
                table: "evidence",
                column: "TimelineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_git_commit_records_Repository_Sha",
                table: "git_commit_records",
                columns: new[] { "Repository", "Sha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legacy_archive_records_ImportRunId_TableName_RowKey",
                table: "legacy_archive_records",
                columns: new[] { "ImportRunId", "TableName", "RowKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legacy_import_runs_SourceHash",
                table: "legacy_import_runs",
                column: "SourceHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_health_records_ProviderName",
                table: "provider_health_records",
                column: "ProviderName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_drafts_ReportDate",
                table: "report_drafts",
                column: "ReportDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_report_sections_ReportDraftId_Key",
                table: "report_sections",
                columns: new[] { "ReportDraftId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scheduler_task_records_TaskKey",
                table: "scheduler_task_records",
                column: "TaskKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seen_artworks_ContentSha256",
                table: "seen_artworks",
                column: "ContentSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seen_artworks_NormalizedUrl",
                table: "seen_artworks",
                column: "NormalizedUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_seen_artworks_Platform_ArtworkId",
                table: "seen_artworks",
                columns: new[] { "Platform", "ArtworkId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anniversaries");

            migrationBuilder.DropTable(
                name: "artworks");

            migrationBuilder.DropTable(
                name: "birthdays");

            migrationBuilder.DropTable(
                name: "calendar_events");

            migrationBuilder.DropTable(
                name: "endgame_cycle_instances");

            migrationBuilder.DropTable(
                name: "endgame_cycle_rules");

            migrationBuilder.DropTable(
                name: "evidence");

            migrationBuilder.DropTable(
                name: "git_commit_records");

            migrationBuilder.DropTable(
                name: "legacy_archive_records");

            migrationBuilder.DropTable(
                name: "legacy_import_runs");

            migrationBuilder.DropTable(
                name: "provider_health_records");

            migrationBuilder.DropTable(
                name: "report_sections");

            migrationBuilder.DropTable(
                name: "review_actions");

            migrationBuilder.DropTable(
                name: "scheduler_task_records");

            migrationBuilder.DropTable(
                name: "seen_artworks");

            migrationBuilder.DropTable(
                name: "timeline_item_revisions");

            migrationBuilder.DropTable(
                name: "timeline_items");

            migrationBuilder.DropTable(
                name: "report_drafts");
        }
    }
}
