using Microsoft.EntityFrameworkCore;
using QimiaoDaily.Core;

namespace QimiaoDaily.Data;

public sealed class QimiaoDailyDbContext(DbContextOptions<QimiaoDailyDbContext> options) : DbContext(options)
{
    public DbSet<TimelineItem> TimelineItems => Set<TimelineItem>();
    public DbSet<EvidenceRecord> Evidence => Set<EvidenceRecord>();
    public DbSet<ReviewAction> ReviewActions => Set<ReviewAction>();
    public DbSet<TimelineItemRevision> TimelineItemRevisions => Set<TimelineItemRevision>();
    public DbSet<LegacyImportRun> LegacyImportRuns => Set<LegacyImportRun>();
    public DbSet<LegacyArchiveRecord> LegacyArchiveRecords => Set<LegacyArchiveRecord>();
    public DbSet<GitCommitRecord> GitCommitRecords => Set<GitCommitRecord>();
    public DbSet<BirthdayEntity> Birthdays => Set<BirthdayEntity>();
    public DbSet<AnniversaryEntity> Anniversaries => Set<AnniversaryEntity>();
    public DbSet<CalendarEventEntity> CalendarEvents => Set<CalendarEventEntity>();
    public DbSet<EndgameCycleRuleEntity> EndgameCycleRules => Set<EndgameCycleRuleEntity>();
    public DbSet<EndgameCycleInstanceEntity> EndgameCycleInstances => Set<EndgameCycleInstanceEntity>();
    public DbSet<ArtworkEntity> Artworks => Set<ArtworkEntity>();
    public DbSet<SeenArtworkEntity> SeenArtworks => Set<SeenArtworkEntity>();
    public DbSet<ArtworkReviewActionEntity> ArtworkReviewActions => Set<ArtworkReviewActionEntity>();
    public DbSet<ArtworkRevisionEntity> ArtworkRevisions => Set<ArtworkRevisionEntity>();
    public DbSet<ArtworkDailyRunEntity> ArtworkDailyRuns => Set<ArtworkDailyRunEntity>();
    public DbSet<ReportDraftEntity> ReportDrafts => Set<ReportDraftEntity>();
    public DbSet<ReportSectionEntity> ReportSections => Set<ReportSectionEntity>();
    public DbSet<ProviderHealthRecord> ProviderHealthRecords => Set<ProviderHealthRecord>();
    public DbSet<SchedulerTaskRecord> SchedulerTaskRecords => Set<SchedulerTaskRecord>();
    public DbSet<ManualEventEntity> ManualEvents => Set<ManualEventEntity>();
    public DbSet<BannerEntity> Banners => Set<BannerEntity>();
    public DbSet<BannerCharacterEntity> BannerCharacters => Set<BannerCharacterEntity>();
    public DbSet<GameVersionEntity> GameVersions => Set<GameVersionEntity>();
    public DbSet<EndgameRuleEntity> EndgameRules => Set<EndgameRuleEntity>();
    public DbSet<EndgameAnchorEntity> EndgameAnchors => Set<EndgameAnchorEntity>();
    public DbSet<EndgameOccurrenceEntity> EndgameOccurrences => Set<EndgameOccurrenceEntity>();
    public DbSet<QimiaoImportRecordEntity> ImportRecords => Set<QimiaoImportRecordEntity>();
    public DbSet<ManualDataAuditEntity> ManualDataAudits => Set<ManualDataAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimelineItem>(entity =>
        {
            entity.ToTable("timeline_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GameCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ItemType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ReviewStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.TimePrecision).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.StartTimePrecision).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.EndTimePrecision).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.StartTimeSource).HasMaxLength(120);
            entity.Property(x => x.EndTimeSource).HasMaxLength(120);
            entity.Property(x => x.StartExpression).HasMaxLength(500);
            entity.Property(x => x.EndExpression).HasMaxLength(500);
            entity.Property(x => x.StartTimeEvidenceKey).HasMaxLength(300);
            entity.Property(x => x.EndTimeEvidenceKey).HasMaxLength(300);
            entity.Property(x => x.GachaPoolKind).HasMaxLength(30);
            entity.Property(x => x.GachaPoolPhase).HasMaxLength(30);
            entity.Property(x => x.GachaGroupKey).HasMaxLength(120);
            entity.Property(x => x.CanonicalIdentity).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ChangeKind).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.DataOrigin).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.TimelineItemId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<EvidenceRecord>(entity =>
        {
            entity.ToTable("evidence");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceUrl).IsRequired();
            entity.Property(x => x.SourceText).IsRequired();
            entity.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(40);
        });
        modelBuilder.Entity<ReviewAction>().ToTable("review_actions").HasKey(x => x.Id);
        modelBuilder.Entity<TimelineItemRevision>().ToTable("timeline_item_revisions").HasKey(x => x.Id);
        modelBuilder.Entity<LegacyImportRun>(entity =>
        {
            entity.ToTable("legacy_import_runs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.SourceHash).IsUnique();
            entity.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        });
        modelBuilder.Entity<LegacyArchiveRecord>(entity =>
        {
            entity.ToTable("legacy_archive_records");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ImportRunId, x.TableName, x.RowKey }).IsUnique();
            entity.Property(x => x.PayloadJson).IsRequired();
        });
        modelBuilder.Entity<GitCommitRecord>(entity => { entity.ToTable("git_commit_records"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.Repository, x.Sha }).IsUnique(); entity.Property(x => x.Repository).HasMaxLength(255).IsRequired(); entity.Property(x => x.Sha).HasMaxLength(64).IsRequired(); });
        modelBuilder.Entity<BirthdayEntity>(entity=>{entity.ToTable("birthdays");entity.HasKey(x=>x.Id);entity.HasIndex(x=>new{x.Character,x.Franchise}).IsUnique();entity.Property(x=>x.CanonicalCharacterNameZhCn).HasMaxLength(200).IsRequired();entity.Property(x=>x.Aliases).HasMaxLength(1000).IsRequired();entity.Property(x=>x.SourceTier).HasMaxLength(40).IsRequired();entity.Property(x=>x.OriginTrace).HasMaxLength(4000).IsRequired();entity.Property(x=>x.DataOrigin).HasConversion<string>().HasMaxLength(20).IsRequired();entity.Property(x=>x.VerificationStatus).HasConversion<string>();});
        modelBuilder.Entity<AnniversaryEntity>(entity=>{entity.ToTable("anniversaries");entity.HasKey(x=>x.Id);entity.Property(x=>x.DataOrigin).HasConversion<string>().HasMaxLength(20).IsRequired();entity.Property(x=>x.Notes).HasMaxLength(4000).IsRequired();});
        modelBuilder.Entity<CalendarEventEntity>(entity=>{entity.ToTable("calendar_events");entity.HasKey(x=>x.Id);entity.HasIndex(x=>new{x.EventDate,x.Title}).IsUnique();});
        modelBuilder.Entity<EndgameCycleRuleEntity>(entity =>
        {
            entity.ToTable("endgame_cycle_rules");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GameCode, x.CanonicalName, x.RuleVersion }).IsUnique();
            entity.Property(x => x.VerificationStatus).HasConversion<string>();
        });
        modelBuilder.Entity<EndgameCycleInstanceEntity>(entity =>
        {
            entity.ToTable("endgame_cycle_instances");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TimelineItemId).IsUnique();
            entity.HasIndex(x => new { x.GameCode, x.CanonicalName, x.StartAt }).IsUnique();
            entity.Property(x => x.VerificationStatus).HasConversion<string>();
            entity.Property(x => x.ReviewStatus).HasConversion<string>();
        });
        modelBuilder.Entity<ArtworkEntity>(entity => { entity.ToTable("artworks"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.Platform, x.ArtworkId }).IsUnique(); entity.HasIndex(x => x.NormalizedUrl).IsUnique(); entity.Property(x => x.ReviewStatus).HasConversion<string>(); });
        modelBuilder.Entity<SeenArtworkEntity>(entity => { entity.ToTable("seen_artworks"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.Platform, x.ArtworkId }).IsUnique(); entity.HasIndex(x => x.NormalizedUrl).IsUnique(); entity.HasIndex(x => x.ContentSha256).IsUnique(); entity.HasIndex(x => x.PerceptualHash).IsUnique(); });
        modelBuilder.Entity<ArtworkReviewActionEntity>(entity => { entity.ToTable("artwork_review_actions"); entity.HasKey(x => x.Id); entity.Property(x => x.Action).HasMaxLength(40).IsRequired(); entity.Property(x => x.Actor).HasMaxLength(120).IsRequired(); entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired(); });
        modelBuilder.Entity<ArtworkRevisionEntity>(entity => { entity.ToTable("artwork_revisions"); entity.HasKey(x => x.Id); entity.Property(x => x.FieldName).HasMaxLength(80).IsRequired(); entity.Property(x => x.OldValue).HasMaxLength(4000).IsRequired(); entity.Property(x => x.NewValue).HasMaxLength(4000).IsRequired(); entity.Property(x => x.Actor).HasMaxLength(120).IsRequired(); entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired(); });
        modelBuilder.Entity<ArtworkDailyRunEntity>(entity => { entity.ToTable("artwork_daily_runs"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.CompletedAt); entity.Property(x => x.Provider).HasMaxLength(120).IsRequired(); entity.Property(x => x.Status).HasMaxLength(40).IsRequired(); });
        modelBuilder.Entity<ReportDraftEntity>(entity => { entity.ToTable("report_drafts"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.ReportDate).IsUnique(); entity.HasMany(x => x.Sections).WithOne().HasForeignKey(x => x.ReportDraftId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<ReportSectionEntity>(entity => { entity.ToTable("report_sections"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.ReportDraftId, x.Key }).IsUnique(); });
        modelBuilder.Entity<ProviderHealthRecord>(entity => { entity.ToTable("provider_health_records"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.ProviderName).IsUnique(); entity.Property(x => x.ProviderName).HasMaxLength(120).IsRequired(); });
        modelBuilder.Entity<SchedulerTaskRecord>(entity => { entity.ToTable("scheduler_task_records"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.TaskKey).IsUnique(); entity.Property(x => x.TaskKey).HasMaxLength(120).IsRequired(); });
        modelBuilder.Entity<ManualEventEntity>(entity => { entity.ToTable("manual_events"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.Game, x.StartAt }); ConfigureManualRecord(entity); });
        modelBuilder.Entity<BannerEntity>(entity => { entity.ToTable("banners"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.Game, x.StartAt }); ConfigureManualRecord(entity); entity.Property(x => x.Type).HasMaxLength(80).IsRequired(); entity.HasMany(x => x.Characters).WithOne().HasForeignKey(x => x.BannerId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<BannerCharacterEntity>(entity => { entity.ToTable("banner_characters"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.BannerId, x.SortOrder }).IsUnique(); entity.Property(x => x.Name).HasMaxLength(200).IsRequired(); });
        modelBuilder.Entity<GameVersionEntity>(entity =>
        {
            entity.ToTable("game_versions"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.Game, x.VersionNumber }).IsUnique();
            entity.Property(x => x.Game).HasMaxLength(80).IsRequired();
            entity.Property(x => x.VersionNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.VersionName).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Origin).HasConversion<string>().HasMaxLength(20).IsRequired();
        });
        modelBuilder.Entity<EndgameRuleEntity>(entity => { entity.ToTable("endgame_rules"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.Game, x.Name }).IsUnique(); entity.HasIndex(x => x.RuleKey).IsUnique(); entity.Property(x => x.RuleKey).HasMaxLength(120).IsRequired(); entity.Property(x => x.ConfigurationJson).IsRequired(); entity.Property(x => x.TimePrecision).HasMaxLength(20).IsRequired(); });
        modelBuilder.Entity<EndgameAnchorEntity>(entity => { entity.ToTable("endgame_anchors"); entity.HasKey(x => x.Id); entity.HasIndex(x => x.RuleId); });
        modelBuilder.Entity<EndgameOccurrenceEntity>(entity => { entity.ToTable("endgame_occurrences"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.RuleId, x.StartAt }).IsUnique(); entity.HasIndex(x => new { x.RuleId, x.Sequence }).IsUnique(); entity.Property(x => x.TimePrecision).HasMaxLength(20).IsRequired(); entity.Property(x => x.Notes).HasMaxLength(4000).IsRequired(); });
        modelBuilder.Entity<QimiaoImportRecordEntity>(entity => { entity.ToTable("qimiao_import_records"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.RecordType, x.RecordId }).IsUnique(); entity.HasIndex(x => new { x.RecordType, x.NaturalKey }).IsUnique(); entity.Property(x => x.RecordType).HasMaxLength(40).IsRequired(); entity.Property(x => x.RecordId).HasMaxLength(300).IsRequired(); entity.Property(x => x.NaturalKey).HasMaxLength(1000).IsRequired(); entity.Property(x => x.PayloadJson).IsRequired(); });
        modelBuilder.Entity<ManualDataAuditEntity>(entity => { entity.ToTable("manual_data_audits"); entity.HasKey(x => x.Id); entity.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt }); entity.Property(x => x.Action).HasMaxLength(40).IsRequired(); });
    }

    private static void ConfigureManualRecord<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity) where TEntity : class
    {
        entity.Property("Game").HasMaxLength(80).IsRequired();
        entity.Property("Name").HasMaxLength(500).IsRequired();
        entity.Property("Notes").HasMaxLength(4000).IsRequired();
        entity.Property("Origin").HasConversion<string>().HasMaxLength(20).IsRequired();
    }
}
